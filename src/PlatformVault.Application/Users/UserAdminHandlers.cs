using System.Security.Cryptography;
using PlatformVault.Application.Abstractions;
using PlatformVault.Application.Auditing;
using PlatformVault.Application.Authorization;
using PlatformVault.Domain.Common;
using PlatformVault.Domain.Identity;
using PlatformVault.Domain.Security;

namespace PlatformVault.Application.Users;

public sealed record CreateUserCommand(string UserName, string DisplayName, string? Email, Guid? AreaId, Guid? ManagerUserId,
    IReadOnlyList<string> Roles);

public sealed record UpdateUserCommand(Guid UserId, string DisplayName, string? Email, Guid? AreaId, Guid? ManagerUserId, bool IsActive);

public sealed record SetUserRolesCommand(Guid UserId, IReadOnlyList<string> Roles);

public sealed record ResetUserPasswordCommand(Guid UserId);

public sealed record UnlockUserCommand(Guid UserId);

public sealed record SearchUsersQuery(string? Text, string? Role, bool? IsActive, PageRequest Page);

public sealed record GetUserQuery(Guid UserId);

public sealed record LookupUsersQuery(string? Text);

public sealed record ListAreasQuery;

public sealed record CreateAreaCommand(string Code, string Name);

/// <summary>Contraseña temporal: se muestra una sola vez al Administrador y obliga a cambiarla en el primer inicio.</summary>
public sealed record TemporaryCredential(Guid UserId, string TemporaryPassword);

/// <summary>Genera contraseñas temporales que cumplen la política IMP-25 con un generador criptográfico.</summary>
public static class TemporaryPasswordGenerator
{
    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lower = "abcdefghijkmnopqrstuvwxyz";
    private const string Digits = "23456789";
    private const string Symbols = "!@#$%*+-=?";

    public static string Generate(int length = 20)
    {
        const string all = Upper + Lower + Digits + Symbols;
        var chars = new char[length];
        chars[0] = Upper[RandomNumberGenerator.GetInt32(Upper.Length)];
        chars[1] = Lower[RandomNumberGenerator.GetInt32(Lower.Length)];
        chars[2] = Digits[RandomNumberGenerator.GetInt32(Digits.Length)];
        chars[3] = Symbols[RandomNumberGenerator.GetInt32(Symbols.Length)];
        for (var i = 4; i < length; i++)
            chars[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
        RandomNumberGenerator.Shuffle(chars.AsSpan());
        return new string(chars);
    }
}

internal static class UserRules
{
    public static void Validate(string? displayName, string? email)
    {
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length > 200)
            throw new ValidationFailure("El nombre visible es obligatorio (máximo 200 caracteres).");
        if (email is { Length: > 256 } || (!string.IsNullOrWhiteSpace(email) && !email.Contains('@', StringComparison.Ordinal)))
            throw new ValidationFailure("El correo no es válido.");
        if (CardDataDetector.ContainsCardNumber(displayName))
            throw new DomainException(DomainErrors.CardDataDetected, "El nombre no puede contener datos de tarjeta (RN-124).");
    }

    public static void Throw(IReadOnlyList<string> errors)
    {
        if (errors.Count > 0)
            throw new ValidationFailure("No se pudo completar la operación.", new Dictionary<string, string[]> { ["user"] = [.. errors] });
    }

    /// <summary>Auditor y Seguridad no pueden ser miembros de grupos ni propietarios (RN-103, §4.3).</summary>
    public static async Task EnsureExclusiveRolesAllowedAsync(IUserDirectory users, Guid userId, IReadOnlyCollection<string> roles, CancellationToken ct)
    {
        if (!roles.Any(r => SystemRoles.Exclusive.Contains(r, StringComparer.Ordinal)))
            return;
        var (groups, owned) = await users.GetAssignmentConstraintsAsync(userId, ct);
        if (groups > 0 || owned > 0)
            throw new DomainException(DomainErrors.RoleConflict,
                "El usuario pertenece a grupos o es propietario de objetos: retírelo antes de asignarle Auditor o Seguridad (RN-103).");
    }
}

/// <summary>Alta de usuario local por el Administrador (Fase 1).</summary>
public sealed class CreateUserHandler(ICurrentUser user, IIdentityService identity, IUnitOfWork unitOfWork, AuditLogger audit)
    : ICommandHandler<CreateUserCommand, TemporaryCredential>
{
    public async Task<TemporaryCredential> HandleAsync(CreateUserCommand command, CancellationToken ct)
    {
        user.Require(Permission.ManageUsers);
        UserRules.Validate(command.DisplayName, command.Email);
        var roles = command.Roles.Count == 0 ? [] : SegregationOfDuties.ValidateRoleSet(user.UserId, Guid.Empty, command.Roles);
        var password = TemporaryPasswordGenerator.Generate();

        await using var tx = await unitOfWork.BeginAsync(ct);
        var (userId, errors) = await identity.CreateUserAsync(
            new NewUser(command.UserName?.Trim() ?? string.Empty, command.DisplayName.Trim(), command.Email?.Trim(), command.AreaId, command.ManagerUserId),
            password, user.UserId, ct);
        UserRules.Throw(errors);
        if (roles.Count > 0)
            UserRules.Throw(await identity.SetRolesAsync(userId, roles, user.UserId, ct));
        await audit.SuccessAsync(AuditActions.UserCreated, "User", userId.ToString(), new { userName = command.UserName, roles }, ct);
        await tx.CommitAsync(ct);
        return new TemporaryCredential(userId, password);
    }
}

/// <summary>Edición y desactivación. Desactivar revoca sesiones y accesos temporales (RN-031).</summary>
public sealed class UpdateUserHandler(ICurrentUser user, IClock clock, IIdentityService identity, IUserDirectory users, ISessionService sessions,
    ITemporaryAccessRepository accesses, IUnitOfWork unitOfWork, AuditLogger audit) : ICommandHandler<UpdateUserCommand, Unit>
{
    public async Task<Unit> HandleAsync(UpdateUserCommand command, CancellationToken ct)
    {
        user.Require(Permission.ManageUsers);
        UserRules.Validate(command.DisplayName, command.Email);
        var existing = await users.GetAsync(command.UserId, ct) ?? throw new NotFoundFailure("usuario");
        if (command.UserId == user.UserId && !command.IsActive)
            throw new DomainException(DomainErrors.SelfAssignment, "No puede desactivar su propia cuenta.");

        await using var tx = await unitOfWork.BeginAsync(ct);
        UserRules.Throw(await identity.UpdateUserAsync(command.UserId,
            new UserUpdate(command.DisplayName.Trim(), command.Email?.Trim(), command.AreaId, command.ManagerUserId, command.IsActive), user.UserId, ct));
        var revoked = 0;
        if (existing.IsActive && !command.IsActive)
        {
            revoked = await accesses.RevokeAllForUserAsync(command.UserId, user.UserId, "UserDisabled", clock.UtcNow, ct);
            await sessions.RevokeAllForUserAsync(command.UserId, "UserDisabled", null, ct);
        }
        await audit.SuccessAsync(AuditActions.UserUpdated, "User", command.UserId.ToString(),
            new { command.IsActive, command.AreaId, command.ManagerUserId, revokedAccesses = revoked }, ct);
        await tx.CommitAsync(ct);
        return Unit.Value;
    }
}

/// <summary>Asignación de roles con segregación de funciones (RN-036, RN-038). Cuatro ojos en roles privilegiados: iteración 1B.</summary>
public sealed class SetUserRolesHandler(ICurrentUser user, IIdentityService identity, IUserDirectory users, ISessionService sessions,
    IUnitOfWork unitOfWork, AuditLogger audit) : ICommandHandler<SetUserRolesCommand, Unit>
{
    public async Task<Unit> HandleAsync(SetUserRolesCommand command, CancellationToken ct)
    {
        user.Require(Permission.ManageUsers);
        var existing = await users.GetAsync(command.UserId, ct) ?? throw new NotFoundFailure("usuario");
        IReadOnlyList<string> roles;
        try
        {
            roles = SegregationOfDuties.ValidateRoleSet(user.UserId, command.UserId, command.Roles);
            await UserRules.EnsureExclusiveRolesAllowedAsync(users, command.UserId, roles, ct);
        }
        catch (DomainException ex)
        {
            await audit.DeniedAsync(AuditActions.UserRolesChanged, "User", command.UserId.ToString(), new { requested = command.Roles, ex.Code }, ct);
            throw;
        }

        await using var tx = await unitOfWork.BeginAsync(ct);
        UserRules.Throw(await identity.SetRolesAsync(command.UserId, roles, user.UserId, ct));
        await audit.SuccessAsync(AuditActions.UserRolesChanged, "User", command.UserId.ToString(), new { before = existing.Roles, after = roles }, ct);
        await tx.CommitAsync(ct);
        await sessions.RevokeAllForUserAsync(command.UserId, "RolesChanged", null, ct);
        return Unit.Value;
    }
}

public sealed class ResetUserPasswordHandler(ICurrentUser user, IIdentityService identity, IUserDirectory users, ISessionService sessions,
    AuditLogger audit) : ICommandHandler<ResetUserPasswordCommand, TemporaryCredential>
{
    public async Task<TemporaryCredential> HandleAsync(ResetUserPasswordCommand command, CancellationToken ct)
    {
        user.Require(Permission.ManageUsers);
        if (command.UserId == user.UserId)
            throw new DomainException(DomainErrors.SelfAssignment, "Use el cambio de contraseña para su propia cuenta.");
        _ = await users.GetAsync(command.UserId, ct) ?? throw new NotFoundFailure("usuario");
        var password = TemporaryPasswordGenerator.Generate();
        UserRules.Throw(await identity.ResetPasswordAsync(command.UserId, password, ct));
        await sessions.RevokeAllForUserAsync(command.UserId, "PasswordReset", null, ct);
        await audit.SuccessAsync(AuditActions.UserPasswordReset, "User", command.UserId.ToString(), null, ct);
        return new TemporaryCredential(command.UserId, password);
    }
}

public sealed class UnlockUserHandler(ICurrentUser user, IIdentityService identity, AuditLogger audit) : ICommandHandler<UnlockUserCommand, Unit>
{
    public async Task<Unit> HandleAsync(UnlockUserCommand command, CancellationToken ct)
    {
        user.Require(Permission.ManageUsers);
        await identity.UnlockAsync(command.UserId, ct);
        await audit.SuccessAsync(AuditActions.UserUnlocked, "User", command.UserId.ToString(), null, ct);
        return Unit.Value;
    }
}

public sealed class SearchUsersHandler(ICurrentUser user, IUserDirectory users) : IQueryHandler<SearchUsersQuery, PagedResult<UserView>>
{
    public Task<PagedResult<UserView>> HandleAsync(SearchUsersQuery query, CancellationToken ct)
    {
        user.Require(Permission.ManageUsers);
        return users.SearchAsync(query.Text, query.Role, query.IsActive, query.Page, ct);
    }
}

public sealed class GetUserHandler(ICurrentUser user, IUserDirectory users) : IQueryHandler<GetUserQuery, UserView>
{
    public async Task<UserView> HandleAsync(GetUserQuery query, CancellationToken ct)
    {
        user.Require(Permission.ManageUsers);
        return await users.GetAsync(query.UserId, ct) ?? throw new NotFoundFailure("usuario");
    }
}

/// <summary>Búsqueda mínima de usuarios activos para elegir propietarios o miembros (sin correo ni roles).</summary>
public sealed class LookupUsersHandler(IUserDirectory users) : IQueryHandler<LookupUsersQuery, IReadOnlyList<UserLookup>>
{
    public Task<IReadOnlyList<UserLookup>> HandleAsync(LookupUsersQuery query, CancellationToken ct)
    {
        var text = query.Text?.Trim();
        if (text is { Length: > 100 })
            throw new ValidationFailure("El texto de búsqueda admite como máximo 100 caracteres.");
        return users.LookupAsync(text, 20, ct);
    }
}

public sealed class ListAreasHandler(IAreaRepository areas) : IQueryHandler<ListAreasQuery, IReadOnlyList<AreaView>>
{
    public Task<IReadOnlyList<AreaView>> HandleAsync(ListAreasQuery query, CancellationToken ct) => areas.GetAllAsync(ct);
}

public sealed class CreateAreaHandler(ICurrentUser user, IAreaRepository areas, AuditLogger audit) : ICommandHandler<CreateAreaCommand, Guid>
{
    public async Task<Guid> HandleAsync(CreateAreaCommand command, CancellationToken ct)
    {
        user.Require(Permission.ManageAreas);
        var code = command.Code?.Trim().ToUpperInvariant();
        var name = command.Name?.Trim();
        if (string.IsNullOrEmpty(code) || code.Length > 30 || string.IsNullOrEmpty(name) || name.Length > 150)
            throw new ValidationFailure("Código (máximo 30) y nombre (máximo 150) son obligatorios.");
        var id = Guid.NewGuid();
        await areas.InsertAsync(id, code, name, ct);
        await audit.SuccessAsync("Area.Created", "Area", id.ToString(), new { code, name }, ct);
        return id;
    }
}

using Microsoft.AspNetCore.Identity;
using PlatformVault.Infrastructure.Persistence;

namespace PlatformVault.Infrastructure.Identity;

/// <summary>Custom store de ASP.NET Core Identity sobre procedimientos almacenados (sin EF Core, IMP-03/IMP-04).</summary>
public sealed class UserStore(StoredProcedures sp) :
    IUserPasswordStore<AppUser>,
    IUserSecurityStampStore<AppUser>,
    IUserLockoutStore<AppUser>,
    IUserRoleStore<AppUser>,
    IUserEmailStore<AppUser>
{
    public async Task<IdentityResult> CreateAsync(AppUser user, CancellationToken cancellationToken)
    {
        await sp.ExecuteAsync("[identity].usp_User_Insert", new
        {
            user.UserId,
            user.UserName,
            user.NormalizedUserName,
            user.Email,
            user.NormalizedEmail,
            user.DisplayName,
            user.PasswordHash,
            user.SecurityStamp,
            user.ConcurrencyStamp,
            user.LockoutEnabled,
            user.LockoutEndUtc,
            user.AccessFailedCount,
            user.IsActive,
            user.AreaId,
            user.ManagerUserId,
            user.PasswordChangedAtUtc,
            user.MustChangePassword,
            CreatedBy = user.ActingUserId,
        }, cancellationToken);
        await SaveHistoryAsync(user, cancellationToken);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> UpdateAsync(AppUser user, CancellationToken cancellationToken)
    {
        var newStamp = Guid.NewGuid().ToString("N");
        await sp.ExecuteAsync("[identity].usp_User_Update", new
        {
            user.UserId,
            user.UserName,
            user.NormalizedUserName,
            user.Email,
            user.NormalizedEmail,
            user.DisplayName,
            user.PasswordHash,
            user.SecurityStamp,
            ExpectedConcurrencyStamp = user.ConcurrencyStamp,
            NewConcurrencyStamp = newStamp,
            user.LockoutEnabled,
            user.LockoutEndUtc,
            user.AccessFailedCount,
            user.IsActive,
            user.AreaId,
            user.ManagerUserId,
            user.PasswordChangedAtUtc,
            user.MustChangePassword,
            ModifiedBy = user.ActingUserId,
        }, cancellationToken);
        user.ConcurrencyStamp = newStamp;
        await SaveHistoryAsync(user, cancellationToken);
        return IdentityResult.Success;
    }

    public Task<IdentityResult> DeleteAsync(AppUser user, CancellationToken cancellationToken) =>
        Task.FromResult(IdentityResult.Failed(new IdentityError { Code = "NotSupported", Description = "Los usuarios no se eliminan; se desactivan." }));

    public Task<AppUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) =>
        Guid.TryParse(userId, out var id)
            ? sp.QuerySingleOrDefaultAsync<AppUser>("[identity].usp_User_GetById", new { UserId = id }, cancellationToken)
            : Task.FromResult<AppUser?>(null);

    public Task<AppUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) =>
        sp.QuerySingleOrDefaultAsync<AppUser>("[identity].usp_User_GetByNormalizedUserName", new { NormalizedUserName = normalizedUserName }, cancellationToken);

    public Task<string> GetUserIdAsync(AppUser user, CancellationToken cancellationToken) => Task.FromResult(user.UserId.ToString());

    public Task<string?> GetUserNameAsync(AppUser user, CancellationToken cancellationToken) => Task.FromResult<string?>(user.UserName);

    public Task SetUserNameAsync(AppUser user, string? userName, CancellationToken cancellationToken)
    {
        user.UserName = userName ?? string.Empty;
        return Task.CompletedTask;
    }

    public Task<string?> GetNormalizedUserNameAsync(AppUser user, CancellationToken cancellationToken) => Task.FromResult<string?>(user.NormalizedUserName);

    public Task SetNormalizedUserNameAsync(AppUser user, string? normalizedName, CancellationToken cancellationToken)
    {
        user.NormalizedUserName = normalizedName ?? string.Empty;
        return Task.CompletedTask;
    }

    public Task SetPasswordHashAsync(AppUser user, string? passwordHash, CancellationToken cancellationToken)
    {
        if (!string.Equals(user.PasswordHash, passwordHash, StringComparison.Ordinal))
        {
            user.PasswordHash = passwordHash;
            user.PasswordChangedAtUtc = DateTime.UtcNow;
            user.PasswordChanged = passwordHash is not null;
        }
        return Task.CompletedTask;
    }

    public Task<string?> GetPasswordHashAsync(AppUser user, CancellationToken cancellationToken) => Task.FromResult(user.PasswordHash);

    public Task<bool> HasPasswordAsync(AppUser user, CancellationToken cancellationToken) => Task.FromResult(user.PasswordHash is not null);

    public Task SetSecurityStampAsync(AppUser user, string stamp, CancellationToken cancellationToken)
    {
        user.SecurityStamp = stamp;
        return Task.CompletedTask;
    }

    public Task<string?> GetSecurityStampAsync(AppUser user, CancellationToken cancellationToken) => Task.FromResult<string?>(user.SecurityStamp);

    public Task<DateTimeOffset?> GetLockoutEndDateAsync(AppUser user, CancellationToken cancellationToken) => Task.FromResult(user.LockoutEndUtc);

    public Task SetLockoutEndDateAsync(AppUser user, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken)
    {
        user.LockoutEndUtc = lockoutEnd;
        return Task.CompletedTask;
    }

    public Task<int> IncrementAccessFailedCountAsync(AppUser user, CancellationToken cancellationToken) => Task.FromResult(++user.AccessFailedCount);

    public Task ResetAccessFailedCountAsync(AppUser user, CancellationToken cancellationToken)
    {
        user.AccessFailedCount = 0;
        return Task.CompletedTask;
    }

    public Task<int> GetAccessFailedCountAsync(AppUser user, CancellationToken cancellationToken) => Task.FromResult(user.AccessFailedCount);

    public Task<bool> GetLockoutEnabledAsync(AppUser user, CancellationToken cancellationToken) => Task.FromResult(user.LockoutEnabled);

    public Task SetLockoutEnabledAsync(AppUser user, bool enabled, CancellationToken cancellationToken)
    {
        user.LockoutEnabled = enabled;
        return Task.CompletedTask;
    }

    public Task AddToRoleAsync(AppUser user, string roleName, CancellationToken cancellationToken) =>
        sp.ExecuteAsync("[identity].usp_UserRole_Insert",
            new { user.UserId, NormalizedRoleName = roleName, AssignedBy = user.ActingUserId }, cancellationToken);

    public Task RemoveFromRoleAsync(AppUser user, string roleName, CancellationToken cancellationToken) =>
        sp.ExecuteAsync("[identity].usp_UserRole_Delete", new { user.UserId, NormalizedRoleName = roleName }, cancellationToken);

    public async Task<IList<string>> GetRolesAsync(AppUser user, CancellationToken cancellationToken) =>
        [.. await sp.QueryAsync<string>("[identity].usp_UserRole_GetRoleNames", new { user.UserId }, cancellationToken)];

    public async Task<bool> IsInRoleAsync(AppUser user, string roleName, CancellationToken cancellationToken)
    {
        var roles = await sp.QueryAsync<string>("[identity].usp_UserRole_GetRoleNames", new { user.UserId }, cancellationToken);
        return roles.Any(r => string.Equals(r.ToUpperInvariant(), roleName, StringComparison.Ordinal));
    }

    public async Task<IList<AppUser>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken) =>
        [.. await sp.QueryAsync<AppUser>("[identity].usp_UserRole_GetUsersInRole", new { NormalizedRoleName = roleName }, cancellationToken)];

    public Task SetEmailAsync(AppUser user, string? email, CancellationToken cancellationToken)
    {
        user.Email = email;
        return Task.CompletedTask;
    }

    public Task<string?> GetEmailAsync(AppUser user, CancellationToken cancellationToken) => Task.FromResult(user.Email);

    public Task<bool> GetEmailConfirmedAsync(AppUser user, CancellationToken cancellationToken) => Task.FromResult(true);

    public Task SetEmailConfirmedAsync(AppUser user, bool confirmed, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<AppUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        sp.QuerySingleOrDefaultAsync<AppUser>("[identity].usp_User_GetByNormalizedEmail", new { NormalizedEmail = normalizedEmail }, cancellationToken);

    public Task<string?> GetNormalizedEmailAsync(AppUser user, CancellationToken cancellationToken) => Task.FromResult(user.NormalizedEmail);

    public Task SetNormalizedEmailAsync(AppUser user, string? normalizedEmail, CancellationToken cancellationToken)
    {
        user.NormalizedEmail = normalizedEmail;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
    }

    private async Task SaveHistoryAsync(AppUser user, CancellationToken ct)
    {
        if (!user.PasswordChanged || user.PasswordHash is null)
            return;
        await sp.ExecuteAsync("[identity].usp_PasswordHistory_Insert", new { user.UserId, user.PasswordHash }, ct);
        user.PasswordChanged = false;
    }
}

/// <summary>IMP-25: no se reutilizan las últimas 4 contraseñas.</summary>
public sealed class PasswordHistoryValidator(StoredProcedures sp) : IPasswordValidator<AppUser>
{
    public const int HistorySize = 4;

    public async Task<IdentityResult> ValidateAsync(UserManager<AppUser> manager, AppUser user, string? password)
    {
        if (string.IsNullOrEmpty(password) || user.UserId == Guid.Empty)
            return IdentityResult.Success;
        var hashes = await sp.QueryAsync<string>("[identity].usp_PasswordHistory_GetRecent", new { user.UserId, Count = HistorySize }, CancellationToken.None);
        foreach (var hash in hashes)
        {
            if (manager.PasswordHasher.VerifyHashedPassword(user, hash, password) != PasswordVerificationResult.Failed)
                return IdentityResult.Failed(new IdentityError
                {
                    Code = "PasswordReused",
                    Description = $"No puede reutilizar ninguna de sus últimas {HistorySize} contraseñas.",
                });
        }
        return IdentityResult.Success;
    }
}

/// <summary>Mensajes de validación de Identity en español.</summary>
public sealed class SpanishIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError PasswordTooShort(int length) => new() { Code = nameof(PasswordTooShort), Description = $"La contraseña debe tener al menos {length} caracteres." };
    public override IdentityError PasswordRequiresDigit() => new() { Code = nameof(PasswordRequiresDigit), Description = "La contraseña debe incluir al menos un dígito." };
    public override IdentityError PasswordRequiresLower() => new() { Code = nameof(PasswordRequiresLower), Description = "La contraseña debe incluir al menos una minúscula." };
    public override IdentityError PasswordRequiresUpper() => new() { Code = nameof(PasswordRequiresUpper), Description = "La contraseña debe incluir al menos una mayúscula." };
    public override IdentityError PasswordRequiresNonAlphanumeric() => new() { Code = nameof(PasswordRequiresNonAlphanumeric), Description = "La contraseña debe incluir al menos un símbolo." };
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => new() { Code = nameof(PasswordRequiresUniqueChars), Description = $"La contraseña debe tener al menos {uniqueChars} caracteres distintos." };
    public override IdentityError PasswordMismatch() => new() { Code = nameof(PasswordMismatch), Description = "La contraseña actual no es correcta." };
    public override IdentityError DuplicateUserName(string userName) => new() { Code = nameof(DuplicateUserName), Description = "El nombre de usuario ya existe." };
    public override IdentityError InvalidUserName(string? userName) => new() { Code = nameof(InvalidUserName), Description = "El nombre de usuario contiene caracteres no permitidos." };
    public override IdentityError ConcurrencyFailure() => new() { Code = nameof(ConcurrencyFailure), Description = "El usuario fue modificado por otra operación. Intente de nuevo." };
}

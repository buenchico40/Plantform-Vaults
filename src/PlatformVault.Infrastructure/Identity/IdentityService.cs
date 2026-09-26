using System.Buffers.Text;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using PlatformVault.Application.Abstractions;
using PlatformVault.Application.Users;
using PlatformVault.Infrastructure.Persistence;

namespace PlatformVault.Infrastructure.Identity;

/// <summary>Política de sesión (IMP-26).</summary>
public static class SessionPolicy
{
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan AbsoluteLifetime = TimeSpan.FromHours(8);
    public static readonly TimeSpan PasswordMaxAge = TimeSpan.FromDays(90);
    public const int TokenBytes = 32;

    public static byte[] Hash(string token)
    {
        Span<byte> raw = stackalloc byte[TokenBytes];
        if (!Base64Url.IsValid(token) || Base64Url.GetMaxDecodedLength(token.Length) < TokenBytes
            || !Base64Url.TryDecodeFromChars(token, raw, out var written) || written != TokenBytes)
            return [];
        return SHA256.HashData(raw);
    }
}

/// <summary>Identidad local de la Fase 1: usuario y contraseña con ASP.NET Core Identity (IMP-04, IMP-17).</summary>
public sealed class IdentityService(UserManager<AppUser> users, StoredProcedures sp, IClock clock) : IIdentityService
{
    public async Task<LoginResult> PasswordSignInAsync(string userName, string password, string? clientIp, string? userAgent, CancellationToken ct)
    {
        var user = await users.FindByNameAsync(userName);
        if (user is null)
        {
            // Mismo coste que una verificación real para no revelar si el usuario existe.
            users.PasswordHasher.VerifyHashedPassword(new AppUser(), DummyHash.Value, password);
            return new LoginResult(LoginStatus.InvalidCredentials);
        }
        if (await users.IsLockedOutAsync(user))
            return new LoginResult(LoginStatus.LockedOut, UserId: user.UserId);
        if (!await users.CheckPasswordAsync(user, password) || !user.IsActive)
        {
            if (user.IsActive)
            {
                await users.AccessFailedAsync(user);
                if (await users.IsLockedOutAsync(user))
                {
                    await RevokeAllAsync(user.UserId, "UserLocked", ct);
                    return new LoginResult(LoginStatus.LockedOut, UserId: user.UserId);
                }
            }
            return new LoginResult(LoginStatus.InvalidCredentials, UserId: user.UserId);
        }

        await users.ResetAccessFailedCountAsync(user);
        var now = clock.UtcNow;
        if (!user.MustChangePassword && (user.PasswordChangedAtUtc is null || now - user.PasswordChangedAtUtc > SessionPolicy.PasswordMaxAge))
        {
            user.MustChangePassword = true; // IMP-25: caducidad a los 90 días.
            await users.UpdateAsync(user);
        }

        var tokenBytes = RandomNumberGenerator.GetBytes(SessionPolicy.TokenBytes);
        var token = Base64Url.EncodeToString(tokenBytes);
        var expires = now + SessionPolicy.AbsoluteLifetime;
        await sp.ExecuteAsync("[identity].usp_Session_Insert", new
        {
            SessionId = Guid.NewGuid(),
            user.UserId,
            TokenHash = SHA256.HashData(tokenBytes),
            NowUtc = now,
            ExpiresAtUtc = expires,
            ClientIp = clientIp,
            UserAgent = userAgent is { Length: > 300 } ? userAgent[..300] : userAgent,
        }, ct);
        CryptographicOperations.ZeroMemory(tokenBytes);
        return new LoginResult(LoginStatus.Succeeded, token, expires, user.MustChangePassword, user.UserId);
    }

    public async Task<bool> ReauthenticateAsync(Guid sessionId, Guid userId, string password, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive || await users.IsLockedOutAsync(user))
            return false;
        if (!await users.CheckPasswordAsync(user, password))
        {
            await users.AccessFailedAsync(user);
            if (await users.IsLockedOutAsync(user))
                await RevokeAllAsync(user.UserId, "UserLocked", ct);
            return false;
        }
        await users.ResetAccessFailedCountAsync(user);
        await sp.ExecuteAsync("[identity].usp_Session_MarkReauthenticated", new { SessionId = sessionId, NowUtc = clock.UtcNow }, ct);
        return true;
    }

    public async Task<IReadOnlyList<string>> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString()) ?? throw new NotFoundFailure("usuario");
        user.ActingUserId = userId;
        var result = await users.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
            return Errors(result);
        user.MustChangePassword = false;
        return Errors(await users.UpdateAsync(user));
    }

    public async Task<(Guid UserId, IReadOnlyList<string> Errors)> CreateUserAsync(NewUser user, string temporaryPassword, Guid actorId,
        CancellationToken ct)
    {
        var appUser = new AppUser
        {
            UserId = Guid.NewGuid(),
            UserName = user.UserName,
            DisplayName = user.DisplayName,
            Email = string.IsNullOrWhiteSpace(user.Email) ? null : user.Email,
            AreaId = user.AreaId,
            ManagerUserId = user.ManagerUserId,
            IsActive = true,
            MustChangePassword = true,
            ActingUserId = actorId,
        };
        var result = await users.CreateAsync(appUser, temporaryPassword);
        return (appUser.UserId, Errors(result));
    }

    public async Task<IReadOnlyList<string>> UpdateUserAsync(Guid userId, UserUpdate update, Guid actorId, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString()) ?? throw new NotFoundFailure("usuario");
        user.DisplayName = update.DisplayName;
        user.AreaId = update.AreaId;
        user.ManagerUserId = update.ManagerUserId;
        user.IsActive = update.IsActive;
        user.ActingUserId = actorId;
        await users.SetEmailAsync(user, string.IsNullOrWhiteSpace(update.Email) ? null : update.Email);
        return Errors(await users.UpdateAsync(user));
    }

    public async Task<IReadOnlyList<string>> SetRolesAsync(Guid userId, IReadOnlyCollection<string> roles, Guid actorId, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString()) ?? throw new NotFoundFailure("usuario");
        user.ActingUserId = actorId;
        var current = await users.GetRolesAsync(user);
        var remove = current.Except(roles, StringComparer.Ordinal).ToList();
        var add = roles.Except(current, StringComparer.Ordinal).ToList();
        if (remove.Count > 0)
        {
            var removed = await users.RemoveFromRolesAsync(user, remove);
            if (!removed.Succeeded) return Errors(removed);
        }
        return add.Count > 0 ? Errors(await users.AddToRolesAsync(user, add)) : [];
    }

    public async Task<IReadOnlyList<string>> ResetPasswordAsync(Guid userId, string temporaryPassword, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString()) ?? throw new NotFoundFailure("usuario");
        var removed = await users.RemovePasswordAsync(user);
        if (!removed.Succeeded) return Errors(removed);
        var added = await users.AddPasswordAsync(user, temporaryPassword);
        if (!added.Succeeded) return Errors(added);
        user.MustChangePassword = true;
        user.AccessFailedCount = 0;
        user.LockoutEndUtc = null;
        return Errors(await users.UpdateAsync(user));
    }

    public async Task UnlockAsync(Guid userId, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString()) ?? throw new NotFoundFailure("usuario");
        user.AccessFailedCount = 0;
        user.LockoutEndUtc = null;
        await users.UpdateAsync(user);
    }

    private Task RevokeAllAsync(Guid userId, string reason, CancellationToken ct) =>
        sp.ExecuteAsync("[identity].usp_Session_RevokeAllForUser", new { UserId = userId, Reason = reason, NowUtc = clock.UtcNow, ExceptSessionId = (Guid?)null }, ct);

    private static IReadOnlyList<string> Errors(IdentityResult result) =>
        result.Succeeded ? [] : result.Errors.Select(e => e.Description).ToList();

    private static class DummyHash
    {
        public static readonly string Value = new PasswordHasher<AppUser>().HashPassword(new AppUser(), Guid.NewGuid().ToString());
    }
}

/// <summary>Sesiones opacas: la base solo guarda el hash SHA-256 del token (IMP-06, IMP-26).</summary>
public sealed class SessionService(StoredProcedures sp, IClock clock) : ISessionService
{
    public async Task<SessionPrincipal?> ValidateAsync(string token, CancellationToken ct)
    {
        var hash = SessionPolicy.Hash(token);
        if (hash.Length == 0)
            return null;
        var row = await sp.QuerySingleOrDefaultAsync<SessionRow>("[identity].usp_Session_Validate",
            new { TokenHash = hash, NowUtc = clock.UtcNow, IdleTimeoutMinutes = (int)SessionPolicy.IdleTimeout.TotalMinutes }, ct);
        return row is null
            ? null
            : new SessionPrincipal(row.SessionId, row.UserId, row.UserName, row.DisplayName, row.AreaId, row.MustChangePassword,
                DbValues.Utc(row.LastReauthUtc), DbValues.Utc(row.ExpiresAtUtc), DbValues.Split(row.Roles));
    }

    public Task RevokeAsync(Guid sessionId, string reason, CancellationToken ct) =>
        sp.ExecuteAsync("[identity].usp_Session_Revoke", new { SessionId = sessionId, Reason = reason, NowUtc = clock.UtcNow }, ct);

    public Task RevokeAllForUserAsync(Guid userId, string reason, Guid? exceptSessionId, CancellationToken ct) =>
        sp.ExecuteAsync("[identity].usp_Session_RevokeAllForUser",
            new { UserId = userId, Reason = reason, NowUtc = clock.UtcNow, ExceptSessionId = exceptSessionId }, ct);

    private sealed class SessionRow
    {
        public Guid SessionId { get; set; }
        public Guid UserId { get; set; }
        public string UserName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public Guid? AreaId { get; set; }
        public bool MustChangePassword { get; set; }
        public DateTime? LastReauthUtc { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public string? Roles { get; set; }
    }
}

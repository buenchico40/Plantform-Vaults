using PlatformVault.Application.Abstractions;
using PlatformVault.Application.Access;
using PlatformVault.Application.Users;
using PlatformVault.Infrastructure.Persistence;
using static PlatformVault.Infrastructure.Persistence.DbValues;

namespace PlatformVault.Infrastructure.Identity;

public sealed class UserDirectory(StoredProcedures sp, IClock clock) : IUserDirectory
{
    public async Task<UserView?> GetAsync(Guid userId, CancellationToken ct)
    {
        var user = await sp.QuerySingleOrDefaultAsync<AppUser>("[identity].usp_User_GetById", new { UserId = userId }, ct);
        if (user is null) return null;
        var roles = await sp.QueryAsync<string>("[identity].usp_UserRole_GetRoleNames", new { UserId = userId }, ct);
        return new UserView(user.UserId, user.UserName, user.DisplayName, user.Email, user.AreaId, user.ManagerUserId, user.IsActive,
            user.LockoutEndUtc > clock.UtcNow, user.MustChangePassword, Utc(user.PasswordChangedAtUtc), Utc(user.CreatedAtUtc), roles);
    }

    public async Task<PagedResult<UserView>> SearchAsync(string? text, string? role, bool? isActive, PageRequest page, CancellationToken ct)
    {
        var rows = await sp.QueryAsync<SearchRow>("[identity].usp_User_Search", new
        {
            Query = string.IsNullOrWhiteSpace(text) ? null : text.Trim(),
            RoleName = string.IsNullOrWhiteSpace(role) ? null : role,
            IsActive = isActive,
            PageNumber = page.SafePage,
            PageSize = page.SafePageSize,
        }, ct);
        var now = clock.UtcNow;
        var items = rows.Select(r => new UserView(r.UserId, r.UserName, r.DisplayName, r.Email, r.AreaId, r.ManagerUserId, r.IsActive,
            r.LockoutEndUtc > now, r.MustChangePassword, Utc(r.PasswordChangedAtUtc), Utc(r.CreatedAtUtc), Split(r.Roles))).ToList();
        return new PagedResult<UserView>(items, page.SafePage, page.SafePageSize, rows.Count == 0 ? 0 : rows[0].TotalCount);
    }

    public Task<IReadOnlyList<UserContact>> GetContactsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct) =>
        userIds.Count == 0
            ? Task.FromResult<IReadOnlyList<UserContact>>([])
            : sp.ContactsAsync("[identity].usp_User_GetContacts", new { UserIds = StoredProcedures.GuidList(userIds) }, ct);

    public Task<IReadOnlyList<UserContact>> GetActiveByRoleAsync(string role, CancellationToken ct) =>
        sp.ContactsAsync("[identity].usp_User_GetActiveByRole", new { NormalizedRoleName = role.ToUpperInvariant() }, ct);

    public async Task<IReadOnlyList<UserLookup>> LookupAsync(string? text, int top, CancellationToken ct)
    {
        var rows = await sp.QueryAsync<LookupRow>("[identity].usp_User_Lookup", new { Text = string.IsNullOrWhiteSpace(text) ? null : text, Top = top }, ct);
        return rows.Select(r => new UserLookup(r.UserId, r.UserName, r.DisplayName)).ToList();
    }

    public async Task<(int GroupMemberships, int OwnedObjects)> GetAssignmentConstraintsAsync(Guid userId, CancellationToken ct)
    {
        var row = await sp.QuerySingleOrDefaultAsync<ConstraintRow>("[identity].usp_User_GetAssignmentConstraints", new { UserId = userId }, ct);
        return (row?.GroupMemberships ?? 0, row?.OwnedObjects ?? 0);
    }

    private sealed class SearchRow
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string? Email { get; set; }
        public bool IsActive { get; set; }
        public Guid? AreaId { get; set; }
        public Guid? ManagerUserId { get; set; }
        public DateTimeOffset? LockoutEndUtc { get; set; }
        public bool MustChangePassword { get; set; }
        public DateTime? PasswordChangedAtUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public string? Roles { get; set; }
        public int TotalCount { get; set; }
    }

    private sealed class LookupRow
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; } = "";
        public string DisplayName { get; set; } = "";
    }

    private sealed class ConstraintRow
    {
        public int GroupMemberships { get; set; }
        public int OwnedObjects { get; set; }
    }
}

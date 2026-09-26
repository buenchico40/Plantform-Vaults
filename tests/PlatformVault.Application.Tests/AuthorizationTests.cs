using PlatformVault.Application.Abstractions;
using PlatformVault.Application.Authorization;
using PlatformVault.Application.Common;
using PlatformVault.Application.Objects;
using PlatformVault.Application.Users;
using PlatformVault.Domain.Access;
using PlatformVault.Domain.Identity;
using PlatformVault.Domain.Objects;

namespace PlatformVault.Application.Tests;

internal sealed class FakeUser(Guid id, Guid? area, params string[] roles) : ICurrentUser
{
    public bool IsAuthenticated => true;
    public Guid UserId { get; } = id;
    public string UserName => "u";
    public string DisplayName => "U";
    public Guid? AreaId { get; } = area;
    public Guid SessionId => Guid.Empty;
    public DateTime? LastReauthenticatedUtc { get; init; }
    public IReadOnlyCollection<string> Roles { get; } = roles;
}

public sealed class ObjectAuthorizationTests
{
    private static readonly Guid Area = Guid.NewGuid();
    private static readonly Guid OtherArea = Guid.NewGuid();
    private static readonly Guid Owner = Guid.NewGuid();

    private static ObjectAuthorizationContext Context(bool viewerIsMember = false, bool groupActive = true, Guid? createdBy = null) => new(
        Guid.NewGuid(), "OBJ-000001", ObjectType.Secret, Criticality.Medium, Sensitivity.Confidential, LifecycleState.Active,
        CustodyMode.Internal, true, Area, Owner, createdBy ?? Guid.NewGuid(), 1,
        [new ObjectGroupInfo(Guid.NewGuid(), "GRP-000001", "Pagos", groupActive, 2, viewerIsMember)], []);

    [Fact]
    public void Custodian_of_the_same_area_outside_groups_cannot_see_the_object_IMP61()
    {
        var user = new FakeUser(Guid.NewGuid(), Area, SystemRoles.Custodian);
        Assert.False(ObjectAuthorizer.IsVisible(Context(), user));
    }

    [Fact]
    public void Custodian_sees_objects_of_own_groups_IMP61()
    {
        var user = new FakeUser(Guid.NewGuid(), OtherArea, SystemRoles.Custodian);
        Assert.True(ObjectAuthorizer.IsVisible(Context(viewerIsMember: true), user));
    }

    [Fact]
    public void Custodian_sees_and_manages_objects_they_registered_IMP61()
    {
        var creator = Guid.NewGuid();
        var user = new FakeUser(creator, Area, SystemRoles.Custodian);
        var context = Context(createdBy: creator);
        Assert.True(ObjectAuthorizer.IsVisible(context, user));
        Assert.True(ObjectAuthorizer.IsAllowed(context, user, ObjectOperation.ManageOwners));
        Assert.True(ObjectAuthorizer.IsAllowed(context, user, ObjectOperation.ManageGroups));
    }

    [Fact]
    public void Inactive_group_does_not_grant_visibility_RN035()
    {
        var user = new FakeUser(Guid.NewGuid(), OtherArea, SystemRoles.Custodian);
        Assert.True(ObjectAuthorizer.IsVisible(Context(viewerIsMember: true), user));
        Assert.False(ObjectAuthorizer.IsVisible(Context(viewerIsMember: true, groupActive: false), user));
    }

    [Theory]
    [InlineData(SystemRoles.Auditor)]
    [InlineData(SystemRoles.Security)]
    [InlineData(SystemRoles.Administrator)]
    public void Global_roles_see_metadata_but_cannot_modify_or_request_RN010(string role)
    {
        var user = new FakeUser(Guid.NewGuid(), OtherArea, role);
        var context = Context();
        Assert.True(ObjectAuthorizer.IsAllowed(context, user, ObjectOperation.View));
        Assert.False(ObjectAuthorizer.IsAllowed(context, user, ObjectOperation.EditMetadata));
        Assert.False(ObjectAuthorizer.IsAllowed(context, user, ObjectOperation.EditValue));
        Assert.False(ObjectAuthorizer.IsAllowed(context, user, ObjectOperation.RequestAccess));
    }

    [Fact]
    public void Security_may_only_reclassify()
    {
        var user = new FakeUser(Guid.NewGuid(), null, SystemRoles.Security);
        Assert.True(ObjectAuthorizer.IsAllowed(Context(), user, ObjectOperation.Reclassify));
        Assert.True(ObjectAuthorizer.IsAllowed(Context(), user, ObjectOperation.ViewAudit));
        Assert.False(ObjectAuthorizer.IsAllowed(Context(), user, ObjectOperation.ChangeState));
    }

    [Fact]
    public void Owner_edits_the_value_and_changes_state_but_does_not_reassign_ownership_IMP62()
    {
        var owner = new FakeUser(Owner, OtherArea);
        Assert.True(ObjectAuthorizer.IsAllowed(Context(), owner, ObjectOperation.EditValue));
        Assert.True(ObjectAuthorizer.IsAllowed(Context(), owner, ObjectOperation.ChangeState));
        Assert.False(ObjectAuthorizer.IsAllowed(Context(), owner, ObjectOperation.ManageOwners));
    }

    [Fact]
    public void Custodian_member_has_full_custodian_permissions_IMP61()
    {
        var user = new FakeUser(Guid.NewGuid(), OtherArea, SystemRoles.Custodian);
        var context = Context(viewerIsMember: true);
        Assert.True(ObjectAuthorizer.IsAllowed(context, user, ObjectOperation.EditValue));
        Assert.True(ObjectAuthorizer.IsAllowed(context, user, ObjectOperation.RequestAccess));
        Assert.True(ObjectAuthorizer.IsAllowed(context, user, ObjectOperation.ChangeState));
        Assert.True(ObjectAuthorizer.IsAllowed(context, user, ObjectOperation.ManageOwners));
    }

    [Fact]
    public void Member_without_operational_role_only_views()
    {
        var user = new FakeUser(Guid.NewGuid(), OtherArea);
        var context = Context(viewerIsMember: true);
        Assert.True(ObjectAuthorizer.IsAllowed(context, user, ObjectOperation.View));
        Assert.False(ObjectAuthorizer.IsAllowed(context, user, ObjectOperation.RequestAccess));
    }

    [Fact]
    public void Scope_is_global_only_for_global_roles()
    {
        Assert.False(new FakeUser(Guid.NewGuid(), Area, SystemRoles.Custodian).Scope().HasGlobalScope);
        Assert.True(new FakeUser(Guid.NewGuid(), Area, SystemRoles.Auditor).Scope().HasGlobalScope);
    }
}

public sealed class FunctionalPermissionTests
{
    [Fact]
    public void Default_deny_for_users_without_roles_RN040() => Assert.Empty(Permissions.For([]));

    [Fact]
    public void Administrator_does_not_read_business_audit_note1()
    {
        var perms = Permissions.For([SystemRoles.Administrator]);
        Assert.DoesNotContain(Permission.ViewAudit, perms);
        Assert.DoesNotContain(Permission.CreateObject, perms);
        Assert.Contains(Permission.ManageUsers, perms);
    }

    [Fact]
    public void Only_security_approves_critical_access_RN122()
    {
        Assert.Contains(Permission.ApproveCriticalAccess, Permissions.For([SystemRoles.Security]));
        Assert.DoesNotContain(Permission.ApproveCriticalAccess, Permissions.For([SystemRoles.Administrator, SystemRoles.Custodian]));
    }

    [Fact]
    public void Custodian_registers_objects_IMP61()
    {
        Assert.Contains(Permission.CreateObject, Permissions.For([SystemRoles.Custodian]));
        Assert.DoesNotContain(Permission.CreateObject, Permissions.For([SystemRoles.Auditor]));
        Assert.DoesNotContain(Permission.CreateObject, Permissions.For([SystemRoles.Security]));
    }

    [Fact]
    public void Union_of_roles_DEC17()
    {
        var perms = Permissions.For([SystemRoles.Administrator, SystemRoles.Custodian]);
        Assert.Contains(Permission.ManageUsers, perms);
        Assert.Contains(Permission.CreateObject, perms);
    }

    [Fact]
    public void Reauthentication_window_is_15_minutes_IMP29()
    {
        var now = DateTime.UtcNow;
        new FakeUser(Guid.NewGuid(), null) { LastReauthenticatedUtc = now.AddMinutes(-14) }.EnsureRecentlyReauthenticated(now);
        Assert.Throws<ReauthenticationRequiredFailure>(() =>
            new FakeUser(Guid.NewGuid(), null) { LastReauthenticatedUtc = now.AddMinutes(-16) }.EnsureRecentlyReauthenticated(now));
        Assert.Throws<ReauthenticationRequiredFailure>(() => new FakeUser(Guid.NewGuid(), null).EnsureRecentlyReauthenticated(now));
    }
}

public sealed class SupportTests
{
    [Fact]
    public void ETag_round_trip_and_rejects_garbage()
    {
        byte[] rowVer = [0, 0, 0, 0, 0, 0, 0x1F, 0x46];
        Assert.Equal(rowVer, ETag.Parse("\"" + ETag.From(rowVer) + "\""));
        Assert.Throws<ValidationFailure>(() => ETag.Parse(null));
        Assert.Throws<ValidationFailure>(() => ETag.Parse("no-base64!"));
        Assert.Throws<ValidationFailure>(() => ETag.Parse(Convert.ToBase64String([1, 2, 3])));
    }

    [Fact]
    public void Temporary_passwords_meet_policy_IMP25()
    {
        for (var i = 0; i < 50; i++)
        {
            var p = TemporaryPasswordGenerator.Generate();
            Assert.True(p.Length >= 15);
            Assert.Contains(p, char.IsUpper);
            Assert.Contains(p, char.IsLower);
            Assert.Contains(p, char.IsDigit);
            Assert.Contains(p, c => !char.IsLetterOrDigit(c));
            Assert.True(p.Distinct().Count() >= 5);
        }
    }

    [Fact]
    public void Page_request_is_bounded()
    {
        Assert.Equal(PageRequest.MaxPageSize, new PageRequest(1, 10_000).SafePageSize);
        Assert.Equal(0, new PageRequest(-3, 10).Offset);
    }

    [Theory]
    [InlineData(PayloadKind.Text, AccessAction.Reveal)]
    [InlineData(PayloadKind.Pkcs12, AccessAction.DownloadPrivateKey)]
    [InlineData(PayloadKind.KeyMaterial, AccessAction.DownloadKeyMaterial)]
    public void Action_follows_value_kind(PayloadKind kind, AccessAction action) => Assert.Equal(action, AccessPolicy.ActionFor(kind));
}

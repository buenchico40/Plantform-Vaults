using System.Security.Cryptography;
using System.Text;
using PlatformVault.Application.Abstractions;
using PlatformVault.Application.Auditing;
using PlatformVault.Application.Objects;
using PlatformVault.Domain.Objects;
using PlatformVault.Infrastructure.Cryptography;
using PlatformVault.Infrastructure.Identity;
using PlatformVault.Infrastructure.Persistence;

namespace PlatformVault.Infrastructure.Tests;

internal sealed class TestClock(DateTime now) : IClock
{
    public DateTime UtcNow { get; set; } = now;
}

[Collection(DatabaseCollection.Name)]
public sealed class RepositoryIntegrationTests(DatabaseFixture db)
{
    private static readonly Guid DefaultArea = Guid.Parse("7A1E0002-0000-4000-8000-000000000001");

    private static async Task<Guid> CreateUserAsync(StoredProcedures sp, string prefix)
    {
        var id = Guid.NewGuid();
        var name = prefix + id.ToString("N")[..8];
        await sp.ExecuteAsync("[identity].usp_User_Insert", new
        {
            UserId = id,
            UserName = name,
            NormalizedUserName = name.ToUpperInvariant(),
            Email = (string?)null,
            NormalizedEmail = (string?)null,
            DisplayName = name,
            PasswordHash = (string?)null,
            SecurityStamp = "s",
            ConcurrencyStamp = "c",
            LockoutEnabled = true,
            LockoutEndUtc = (DateTimeOffset?)null,
            AccessFailedCount = 0,
            IsActive = true,
            AreaId = DefaultArea,
            ManagerUserId = (Guid?)null,
            PasswordChangedAtUtc = (DateTime?)null,
            MustChangePassword = false,
            CreatedBy = (Guid?)null,
        }, CancellationToken.None);
        return id;
    }

    private static ManagedObject NewSecret(Guid owner, string name) => ManagedObject.Register(new ObjectRegistration(ObjectType.Secret, "ApiKey", name,
        null, Criticality.Medium, Sensitivity.Confidential, DeploymentEnvironment.Development, DefaultArea, owner, CustodyMode.Internal,
        DateTime.UtcNow.AddDays(60), false, null), Guid.NewGuid());

    [Fact]
    public async Task Audit_chain_is_verified_and_append_only_RN075_RN076()
    {
        var (session, sp) = db.Open();
        await using var _ = session;
        var audit = new AuditRepository(sp);
        for (var i = 0; i < 3; i++)
        {
            await audit.AppendAsync(new AuditEntry(Guid.NewGuid(), DateTime.UtcNow, ActorTypes.System, null, "test", "Test.Event", "Test", i.ToString(),
                AuditResults.Success, "127.0.0.1", Guid.NewGuid(), "{\"n\":" + i + "}"), CancellationToken.None);
        }
        var status = await audit.VerifyChainAsync(DateTime.UtcNow, CancellationToken.None);
        Assert.True(status.IsIntact);
        Assert.True(status.EventsVerified >= 3);
    }

    [Fact]
    public async Task Audit_is_rolled_back_with_the_business_change_RN079()
    {
        var (session, sp) = db.Open();
        await using var _ = session;
        var marker = Guid.NewGuid().ToString();
        var audit = new AuditRepository(sp);
        await using (var tx = await session.BeginAsync(CancellationToken.None))
        {
            await audit.AppendAsync(new AuditEntry(Guid.NewGuid(), DateTime.UtcNow, ActorTypes.System, null, "test", "Test.Rollback", "Test", marker,
                AuditResults.Success, null, null, null), CancellationToken.None);
            // Sin Commit: se revierte al salir.
        }
        var events = await audit.ListByResourceAsync("Test", marker, CancellationToken.None);
        Assert.Empty(events);
        Assert.True((await audit.VerifyChainAsync(DateTime.UtcNow, CancellationToken.None)).IsIntact);
    }

    [Fact]
    public async Task Optimistic_concurrency_and_versioning_RN092()
    {
        var (session, sp) = db.Open();
        await using var _ = session;
        var owner = await CreateUserAsync(sp, "own");
        var objects = new ObjectRepository(sp);
        var obj = NewSecret(owner, "conc-" + Guid.NewGuid().ToString("N")[..8]);
        var created = await objects.InsertAsync(obj, owner, DateTime.UtcNow, "Alta", "[]", [], CancellationToken.None);
        Assert.StartsWith("OBJ-", created.Code, StringComparison.Ordinal);

        var loaded = (await objects.LoadAsync(obj.ObjectId, CancellationToken.None))!;
        var stale = loaded.RowVer.ToArray();
        loaded.UpdateMetadata(new MetadataChange(loaded.Name + "-v2", "d", loaded.ExpirationDate, false, null));
        var updated = await objects.UpdateMetadataAsync(loaded, stale, owner, DateTime.UtcNow, "Cambio", "[\"name\"]", false, CancellationToken.None);
        Assert.Equal(2, updated.CurrentVersion);
        await Assert.ThrowsAsync<ConcurrencyFailure>(() =>
            objects.UpdateMetadataAsync(loaded, stale, owner, DateTime.UtcNow, "Viejo", "[]", false, CancellationToken.None));

        var versions = await objects.ListVersionsAsync(obj.ObjectId, new VisibilityScope(owner, false), CancellationToken.None);
        Assert.Equal([2, 1], versions.Select(v => v.Version));
    }

    [Fact]
    public async Task Database_visibility_filter_blocks_out_of_scope_users_RN010()
    {
        var (session, sp) = db.Open();
        await using var _ = session;
        var owner = await CreateUserAsync(sp, "own");
        var creator = await CreateUserAsync(sp, "cre");
        var stranger = await CreateUserAsync(sp, "str");
        var objects = new ObjectRepository(sp);
        var name = "vis-" + Guid.NewGuid().ToString("N")[..8];
        var obj = NewSecret(owner, name);
        await objects.InsertAsync(obj, creator, DateTime.UtcNow, "Alta", "[]", [], CancellationToken.None);

        var criteria = new ObjectSearchCriteria { Text = name };
        var page = new PageRequest();
        Assert.Equal(1, (await objects.SearchAsync(criteria, page, new VisibilityScope(owner, false), DateTime.UtcNow, CancellationToken.None)).TotalItems);
        Assert.Equal(0, (await objects.SearchAsync(criteria, page, new VisibilityScope(stranger, false), DateTime.UtcNow, CancellationToken.None)).TotalItems);
        Assert.Null(await objects.GetDetailAsync(obj.ObjectId, new VisibilityScope(stranger, false), DateTime.UtcNow, CancellationToken.None));
        // IMP-61: quien registró el objeto lo ve aunque no sea propietario ni miembro de sus grupos.
        Assert.NotNull(await objects.GetDetailAsync(obj.ObjectId, new VisibilityScope(creator, false), DateTime.UtcNow, CancellationToken.None));
    }

    [Fact]
    public async Task Groups_chosen_at_creation_grant_visibility_in_version_1_IMP63()
    {
        var (session, sp) = db.Open();
        await using var _ = session;
        var creator = await CreateUserAsync(sp, "cre");
        var member = await CreateUserAsync(sp, "mem");
        var stranger = await CreateUserAsync(sp, "str");
        var groups = new GroupRepository(sp);
        var groupId = Guid.NewGuid();
        await groups.InsertAsync(groupId, "grp-" + Guid.NewGuid().ToString("N")[..8], null, null, creator, creator, DateTime.UtcNow, CancellationToken.None);
        await groups.UpsertMemberAsync(groupId, member, false, creator, DateTime.UtcNow, CancellationToken.None);

        var objects = new ObjectRepository(sp);
        var obj = NewSecret(creator, "grp-" + Guid.NewGuid().ToString("N")[..8]);
        var created = await objects.InsertAsync(obj, creator, DateTime.UtcNow, "Alta", "[]", [groupId], CancellationToken.None);

        Assert.Equal(1, created.CurrentVersion);
        var detail = await objects.GetDetailAsync(obj.ObjectId, new VisibilityScope(member, false), DateTime.UtcNow, CancellationToken.None);
        Assert.NotNull(detail);
        Assert.Equal([groupId], detail.Groups.Select(g => g.Id));
        Assert.Null(await objects.GetDetailAsync(obj.ObjectId, new VisibilityScope(stranger, false), DateTime.UtcNow, CancellationToken.None));
    }

    [Fact]
    public async Task Duplicate_names_are_detected_RN005()
    {
        var (session, sp) = db.Open();
        await using var _ = session;
        var owner = await CreateUserAsync(sp, "own");
        var objects = new ObjectRepository(sp);
        var name = "dup-" + Guid.NewGuid().ToString("N")[..8];
        await objects.InsertAsync(NewSecret(owner, name), owner, DateTime.UtcNow, "Alta", "[]", [], CancellationToken.None);
        var (exists, _) = await objects.ExistsDuplicateAsync(ObjectType.Secret, DeploymentEnvironment.Development, DefaultArea, name, null, null, CancellationToken.None);
        Assert.True(exists);
        await Assert.ThrowsAsync<ConflictFailure>(() => objects.InsertAsync(NewSecret(owner, name), owner, DateTime.UtcNow, "Alta", "[]", [], CancellationToken.None));
    }

    [Fact]
    public async Task Vault_stores_only_ciphertext_IMP18()
    {
        var (session, sp) = db.Open();
        await using var _ = session;
        var owner = await CreateUserAsync(sp, "own");
        var objects = new ObjectRepository(sp);
        var obj = NewSecret(owner, "vault-" + Guid.NewGuid().ToString("N")[..8]);
        obj.HasPayload = true;
        await objects.InsertAsync(obj, owner, DateTime.UtcNow, "Alta", "[]", [], CancellationToken.None);

        using var kek = TestCertificates.Rsa();
        using var crypto = new EnvelopeEncryption(kek, []);
        var store = new SecretPayloadStore(sp);
        await store.InsertAsync(crypto.Encrypt(Encoding.UTF8.GetBytes("clave-de-prueba"), obj.ObjectId, 1, 0, PayloadKind.Text), owner, CancellationToken.None);

        var stored = (await store.GetLatestAsync(obj.ObjectId, 0, CancellationToken.None))!;
        Assert.DoesNotContain("clave-de-prueba", Encoding.UTF8.GetString(stored.Ciphertext), StringComparison.Ordinal);
        Assert.Equal("clave-de-prueba", Encoding.UTF8.GetString(crypto.Decrypt(stored)));
    }

    [Fact]
    public async Task Public_certificate_returns_latest_version_IMP42()
    {
        var (session, sp) = db.Open();
        await using var _ = session;
        var owner = await CreateUserAsync(sp, "own");
        var objects = new ObjectRepository(sp);
        var obj = NewSecret(owner, "cert-" + Guid.NewGuid().ToString("N")[..8]);
        await objects.InsertAsync(obj, owner, DateTime.UtcNow, "Alta", "[]", [], CancellationToken.None);
        Assert.Null(await objects.GetPublicCertificateAsync(obj.ObjectId, CancellationToken.None));
        await objects.InsertPublicCertificateAsync(obj.ObjectId, 1, [1, 2, 3], CancellationToken.None);
        await objects.InsertPublicCertificateAsync(obj.ObjectId, 2, [4, 5, 6], CancellationToken.None);
        Assert.Equal([4, 5, 6], await objects.GetPublicCertificateAsync(obj.ObjectId, CancellationToken.None));
    }

    [Fact]
    public async Task Session_expires_after_idle_timeout_and_revocation_IMP26()
    {
        var (session, sp) = db.Open();
        await using var _ = session;
        var user = await CreateUserAsync(sp, "ses");
        var raw = RandomNumberGenerator.GetBytes(SessionPolicy.TokenBytes);
        var token = System.Buffers.Text.Base64Url.EncodeToString(raw);
        var start = DateTime.UtcNow;
        await sp.ExecuteAsync("[identity].usp_Session_Insert", new
        {
            SessionId = Guid.NewGuid(),
            UserId = user,
            TokenHash = SHA256.HashData(raw),
            NowUtc = start,
            ExpiresAtUtc = start.AddHours(8),
            ClientIp = "127.0.0.1",
            UserAgent = "tests",
        }, CancellationToken.None);

        var clock = new TestClock(start.AddMinutes(10));
        var sessions = new SessionService(sp, clock);
        var principal = await sessions.ValidateAsync(token, CancellationToken.None);
        Assert.NotNull(principal);
        Assert.Equal(user, principal!.UserId);
        Assert.Null(await sessions.ValidateAsync(token[..^2] + "AA", CancellationToken.None));

        clock.UtcNow = start.AddMinutes(10 + 16);
        Assert.Null(await sessions.ValidateAsync(token, CancellationToken.None));
        clock.UtcNow = start.AddMinutes(27);
        Assert.Null(await sessions.ValidateAsync(token, CancellationToken.None)); // revocada, no se reactiva
    }
}

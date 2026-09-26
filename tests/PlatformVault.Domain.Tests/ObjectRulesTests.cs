using PlatformVault.Domain.Common;
using PlatformVault.Domain.Objects;
using PlatformVault.Domain.Security;

namespace PlatformVault.Domain.Tests;

public sealed class ObjectRulesTests
{
    private static readonly Guid Area = Guid.NewGuid();
    private static readonly Guid Owner = Guid.NewGuid();

    private static ObjectRegistration Secret(Criticality criticality = Criticality.Medium, Sensitivity sensitivity = Sensitivity.Confidential,
        string name = "api-pagos", CustodyMode custody = CustodyMode.Internal, DateTime? expiration = null) =>
        new(ObjectType.Secret, "ApiKey", name, null, criticality, sensitivity, DeploymentEnvironment.Production, Area, Owner,
            custody, expiration ?? DateTime.UtcNow.AddDays(90), false, null);

    [Fact]
    public void Register_creates_draft_with_version_one()
    {
        var obj = ManagedObject.Register(Secret(), Guid.NewGuid());
        Assert.Equal(LifecycleState.Draft, obj.LifecycleState);
        Assert.Equal(1, obj.CurrentVersion);
    }

    [Fact]
    public void Custodied_value_cannot_be_below_confidential_RN004()
    {
        var ex = Assert.Throws<DomainException>(() => ManagedObject.Register(Secret(sensitivity: Sensitivity.Internal), Guid.NewGuid()));
        Assert.Equal(DomainErrors.SensitivityTooLow, ex.Code);
    }

    [Fact]
    public void Metadata_only_objects_may_be_internal()
    {
        var obj = ManagedObject.Register(Secret(sensitivity: Sensitivity.Internal, custody: CustodyMode.MetadataOnly), Guid.NewGuid());
        Assert.Throws<DomainException>(() => obj.EnsureCanStorePayload());
    }

    [Fact]
    public void Swift_certificates_must_be_critical_RN019()
    {
        var data = new ObjectRegistration(ObjectType.Certificate, "Swift", "swift-bic", null, Criticality.High, Sensitivity.Restricted,
            DeploymentEnvironment.Production, Area, Owner, CustodyMode.Internal, DateTime.UtcNow.AddDays(300), false, null);
        Assert.Throws<DomainException>(() => ManagedObject.Register(data, Guid.NewGuid()));
    }

    [Fact]
    public void Credentials_require_target_system_and_account_RN013()
    {
        var data = new ObjectRegistration(ObjectType.Credential, "Database", "db-pagos", null, Criticality.High, Sensitivity.Confidential,
            DeploymentEnvironment.Production, Area, Owner, CustodyMode.Internal, DateTime.UtcNow.AddDays(90), false,
            new Dictionary<string, string> { ["targetSystem"] = "sql01" });
        var ex = Assert.Throws<DomainException>(() => ManagedObject.Register(data, Guid.NewGuid()));
        Assert.Contains("accountName", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Card_numbers_in_metadata_are_rejected_RN124()
    {
        var ex = Assert.Throws<DomainException>(() => ManagedObject.Register(Secret(name: "tarjeta 4111 1111 1111 1111"), Guid.NewGuid()));
        Assert.Equal(DomainErrors.CardDataDetected, ex.Code);
    }

    [Fact]
    public void Unknown_subtype_is_rejected_RN002()
    {
        var data = Secret() with { Subtype = "Inventado" };
        Assert.Throws<DomainException>(() => ManagedObject.Register(data, Guid.NewGuid()));
    }

    [Fact]
    public void Activation_requires_an_owner_RN087()
    {
        var obj = ManagedObject.Register(Secret() with { OwnerId = null }, Guid.NewGuid());
        var ex = Assert.Throws<DomainException>(() => obj.ChangeState(LifecycleState.Active, new GroupCoverage(1, 3)));
        Assert.Equal(DomainErrors.OwnersRequired, ex.Code);
    }

    [Fact]
    public void Activation_requires_expiration_or_justification_RN063()
    {
        var obj = ManagedObject.Register(Secret() with { ExpirationDate = null }, Guid.NewGuid());
        var ex = Assert.Throws<DomainException>(() => obj.ChangeState(LifecycleState.Active, new GroupCoverage(1, 3)));
        Assert.Equal(DomainErrors.ExpirationRequired, ex.Code);
    }

    [Theory]
    [InlineData(Criticality.Critical, Sensitivity.Confidential)]
    [InlineData(Criticality.Medium, Sensitivity.Restricted)]
    public void Critical_or_restricted_needs_group_with_two_members_RN104(Criticality criticality, Sensitivity sensitivity)
    {
        var obj = ManagedObject.Register(Secret(criticality, sensitivity), Guid.NewGuid());
        Assert.Throws<DomainException>(() => obj.ChangeState(LifecycleState.Active, new GroupCoverage(1, 1)));
        obj.ChangeState(LifecycleState.Active, new GroupCoverage(1, 2));
        Assert.Equal(LifecycleState.Active, obj.LifecycleState);
    }

    [Theory]
    [InlineData(LifecycleState.Draft, LifecycleState.Suspended, false)]
    [InlineData(LifecycleState.Active, LifecycleState.Suspended, true)]
    [InlineData(LifecycleState.Deactivated, LifecycleState.Active, true)]
    [InlineData(LifecycleState.Deactivated, LifecycleState.Suspended, false)]
    [InlineData(LifecycleState.Active, LifecycleState.Draft, false)]
    public void Lifecycle_transitions_RN007(LifecycleState from, LifecycleState to, bool allowed) =>
        Assert.Equal(allowed, LifecycleTransitions.IsAllowed(from, to));

    [Fact]
    public void Only_security_downgrades_a_critical_object_RN107()
    {
        var obj = ManagedObject.Register(Secret(Criticality.Critical), Guid.NewGuid());
        Assert.Throws<DomainException>(() => obj.Reclassify(Criticality.High, Sensitivity.Confidential, actorIsSecurity: false));
        var changed = obj.Reclassify(Criticality.High, Sensitivity.Confidential, actorIsSecurity: true);
        Assert.Contains("criticality", changed);
    }

    [Fact]
    public void Metadata_update_reports_changed_fields_only()
    {
        var obj = ManagedObject.Register(Secret(), Guid.NewGuid());
        var changed = obj.UpdateMetadata(new MetadataChange("api-pagos-v2", null, obj.ExpirationDate, false, null));
        Assert.Equal(["name"], changed);
    }

    [Fact]
    public void Certificate_expiration_cannot_be_edited_RN016()
    {
        var data = new ObjectRegistration(ObjectType.Certificate, "SslTls", "web", null, Criticality.High, Sensitivity.Restricted,
            DeploymentEnvironment.Production, Area, Owner, CustodyMode.Internal, DateTime.UtcNow.AddDays(200), false, null, new string('A', 64));
        var obj = ManagedObject.Register(data, Guid.NewGuid());
        Assert.Throws<DomainException>(() => obj.UpdateMetadata(new MetadataChange("web", null, DateTime.UtcNow.AddDays(500), false, null)));
    }

    [Theory]
    [InlineData("4111111111111111", true)]
    [InlineData("4111-1111-1111-1111", true)]
    [InlineData("5500 0000 0000 0004", true)]
    [InlineData("4111111111111112", false)]
    [InlineData("serial 0123456789", false)]
    [InlineData("12345678901234567890123", false)]
    [InlineData(null, false)]
    public void Card_detector_uses_luhn_on_whole_sequences(string? text, bool expected) =>
        Assert.Equal(expected, CardDataDetector.ContainsCardNumber(text));
}

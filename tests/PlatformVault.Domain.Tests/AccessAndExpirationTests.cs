using PlatformVault.Domain.Access;
using PlatformVault.Domain.Common;
using PlatformVault.Domain.Expiration;
using PlatformVault.Domain.Identity;
using PlatformVault.Domain.Objects;

namespace PlatformVault.Domain.Tests;

public sealed class AccessRulesTests
{
    private static readonly DateTime Now = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);
    private const string Justification = "Rotación de credenciales del servicio de pagos";

    private static ManagedObject ActiveSecret(Criticality criticality, Sensitivity sensitivity)
    {
        var obj = ManagedObject.Register(new ObjectRegistration(ObjectType.Secret, "ApiKey", "x", null, criticality, sensitivity,
            DeploymentEnvironment.Production, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CustodyMode.Internal, Now.AddDays(90), false, null),
            Guid.NewGuid());
        obj.HasPayload = true;
        obj.ChangeState(LifecycleState.Active, new GroupCoverage(1, 2));
        return obj;
    }

    [Theory]
    [InlineData(Criticality.Critical, Sensitivity.Confidential, ApproverKind.Security, 240)]
    [InlineData(Criticality.Critical, Sensitivity.Restricted, ApproverKind.Security, 240)]
    [InlineData(Criticality.High, Sensitivity.Restricted, ApproverKind.GroupPeer, 240)]
    [InlineData(Criticality.Low, Sensitivity.Confidential, ApproverKind.Owner, 480)]
    public void Approval_policy_RN045(Criticality criticality, Sensitivity sensitivity, ApproverKind kind, int maxMinutes)
    {
        var rule = AccessPolicy.Resolve(criticality, sensitivity);
        Assert.Equal(kind, rule.ApproverKind);
        Assert.Equal(maxMinutes, rule.MaxDuration.TotalMinutes);
    }

    [Fact]
    public void Public_or_internal_objects_have_no_value_to_reveal() =>
        Assert.Throws<DomainException>(() => AccessPolicy.Resolve(Criticality.Low, Sensitivity.Internal));

    [Fact]
    public void Valid_request_expires_after_72_hours_RN049()
    {
        var request = AccessRequestRules.ValidateNew(ActiveSecret(Criticality.Low, Sensitivity.Confidential), AccessAction.Reveal, Justification, null, 60, Now);
        Assert.Equal(Now.AddHours(72), request.PendingExpiresAtUtc);
        Assert.Equal(ApproverKind.Owner, request.ApproverKind);
    }

    [Fact]
    public void Justification_needs_20_characters_RN047()
    {
        var ex = Assert.Throws<DomainException>(() =>
            AccessRequestRules.ValidateNew(ActiveSecret(Criticality.Low, Sensitivity.Confidential), AccessAction.Reveal, "corta", null, 60, Now));
        Assert.Equal(DomainErrors.JustificationTooShort, ex.Code);
    }

    [Fact]
    public void Duration_cannot_exceed_policy_RN048()
    {
        var ex = Assert.Throws<DomainException>(() =>
            AccessRequestRules.ValidateNew(ActiveSecret(Criticality.Critical, Sensitivity.Restricted), AccessAction.Reveal, Justification, null, 241, Now));
        Assert.Equal(DomainErrors.DurationExceeded, ex.Code);
    }

    [Fact]
    public void Requests_only_on_active_objects_RN050()
    {
        var obj = ActiveSecret(Criticality.Low, Sensitivity.Confidential);
        obj.ChangeState(LifecycleState.Suspended, new GroupCoverage(1, 2));
        Assert.Throws<DomainException>(() => AccessRequestRules.ValidateNew(obj, AccessAction.Reveal, Justification, null, 60, Now));
    }

    [Fact]
    public void Action_must_match_value_kind()
    {
        Assert.Throws<DomainException>(() =>
            AccessRequestRules.ValidateNew(ActiveSecret(Criticality.Low, Sensitivity.Confidential), AccessAction.DownloadKeyMaterial, Justification, null, 60, Now));
    }

    [Fact]
    public void No_self_approval_RN054()
    {
        var id = Guid.NewGuid();
        var ex = Assert.Throws<DomainException>(() => AccessRequestRules.ValidateDecision(id, id, Decision.Approved, null));
        Assert.Equal(DomainErrors.SelfApproval, ex.Code);
    }

    [Fact]
    public void Rejection_requires_comment_RN051() =>
        Assert.Throws<DomainException>(() => AccessRequestRules.ValidateDecision(Guid.NewGuid(), Guid.NewGuid(), Decision.Rejected, " "));

    [Fact]
    public void Past_start_begins_at_approval_time()
    {
        var (start, end) = AccessRequestRules.AccessWindow(Now.AddHours(-1), 60, Now);
        Assert.Equal(Now, start);
        Assert.Equal(Now.AddMinutes(60), end);
    }
}

public sealed class ExpirationRulesTests
{
    private static readonly DateTime Now = new(2026, 9, 25, 0, 0, 0, DateTimeKind.Utc);
    private static readonly ThresholdSchedule Default = ThresholdSchedule.Parse("180,120,90,60,30,15,7,1");

    [Theory]
    [InlineData(-1, Criticality.Low, AlertSeverity.Critical)]
    [InlineData(5, Criticality.Low, AlertSeverity.High)]
    [InlineData(25, Criticality.Critical, AlertSeverity.High)]
    [InlineData(25, Criticality.Medium, AlertSeverity.Medium)]
    [InlineData(100, Criticality.Critical, AlertSeverity.Low)]
    public void Severity_RN065(int days, Criticality criticality, AlertSeverity expected) =>
        Assert.Equal(expected, ExpirationEvaluator.Severity(days, criticality));

    [Fact]
    public void Alert_key_is_the_lowest_reached_threshold_RN064()
    {
        var alert = ExpirationEvaluator.Evaluate(Now.AddDays(20), Criticality.Medium, Default, Now);
        Assert.Equal("T30", alert!.AlertKey);
        Assert.Equal((byte)1, alert.InitialLevel);
    }

    [Fact]
    public void No_alert_before_first_threshold() =>
        Assert.Null(ExpirationEvaluator.Evaluate(Now.AddDays(200), Criticality.Medium, Default, Now));

    [Fact]
    public void Expired_objects_raise_a_daily_critical_alert_up_to_N3_RN068_RN071()
    {
        var alert = ExpirationEvaluator.Evaluate(Now.AddDays(-2), Criticality.Low, Default, Now);
        Assert.Equal("EXP-20260925", alert!.AlertKey);
        Assert.Equal(AlertSeverity.Critical, alert.Severity);
        Assert.Equal((byte)3, alert.InitialLevel);
    }

    [Fact]
    public void Critical_objects_within_30_days_go_to_N3_RN071() =>
        Assert.Equal((byte)3, ExpirationEvaluator.Evaluate(Now.AddDays(10), Criticality.Critical, Default, Now)!.InitialLevel);

    [Fact]
    public void Critical_and_high_keep_30_7_1_RN061()
    {
        Assert.Throws<DomainException>(() => ThresholdSchedule.Parse("90,60,30").ValidateFor(Criticality.High));
        Assert.Throws<DomainException>(() => ThresholdSchedule.Parse("90,60").ValidateFor(null));
        ThresholdSchedule.Parse("60,15").ValidateFor(Criticality.Low);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("400")]
    [InlineData("abc")]
    [InlineData("")]
    public void Invalid_thresholds(string text) => Assert.Throws<DomainException>(() => ThresholdSchedule.Parse(text));

    [Fact]
    public void Most_specific_policy_wins_RN062()
    {
        ExpirationPolicyDefinition P(ObjectType? t, Criticality? c, string name) => new(Guid.NewGuid(), name, t, c, Default, true);
        var policies = new[] { P(null, null, "global"), P(ObjectType.Certificate, null, "tipo"), P(ObjectType.Certificate, Criticality.Critical, "tipo+crit"), P(null, Criticality.Critical, "crit") };
        Assert.Equal("tipo+crit", ExpirationEvaluator.SelectPolicy(policies, ObjectType.Certificate, Criticality.Critical)!.Name);
        Assert.Equal("tipo", ExpirationEvaluator.SelectPolicy(policies, ObjectType.Certificate, Criticality.Low)!.Name);
        Assert.Equal("crit", ExpirationEvaluator.SelectPolicy(policies, ObjectType.Secret, Criticality.Critical)!.Name);
        Assert.Equal("global", ExpirationEvaluator.SelectPolicy(policies, ObjectType.Secret, Criticality.Low)!.Name);
    }

    [Fact]
    public void Escalation_stops_at_level_4_RN069()
    {
        Assert.Equal((byte)2, EscalationPolicy.NextLevel(1));
        Assert.Equal((byte)4, EscalationPolicy.NextLevel(4));
        Assert.Equal(TimeSpan.FromHours(72), EscalationPolicy.WindowFor(AlertSeverity.Medium));
    }
}

public sealed class SegregationOfDutiesTests
{
    private static readonly Guid Admin = Guid.NewGuid();
    private static readonly Guid Target = Guid.NewGuid();

    [Fact]
    public void Multiple_operational_roles_are_allowed_DEC17() =>
        Assert.Equal(2, SegregationOfDuties.ValidateRoleSet(Admin, Target, [SystemRoles.Administrator, SystemRoles.Custodian]).Count);

    [Theory]
    [InlineData(SystemRoles.Auditor)]
    [InlineData(SystemRoles.Security)]
    public void Exclusive_roles_cannot_be_combined_RN036(string exclusive)
    {
        var ex = Assert.Throws<DomainException>(() => SegregationOfDuties.ValidateRoleSet(Admin, Target, [exclusive, SystemRoles.Custodian]));
        Assert.Equal(DomainErrors.RoleConflict, ex.Code);
    }

    [Fact]
    public void Owner_role_is_derived_not_assigned_RN037() =>
        Assert.Throws<DomainException>(() => SegregationOfDuties.ValidateRoleSet(Admin, Target, [SystemRoles.Owner]));

    [Fact]
    public void No_self_assignment_RN038() =>
        Assert.Throws<DomainException>(() => SegregationOfDuties.ValidateRoleSet(Admin, Admin, [SystemRoles.Custodian]));

    [Fact]
    public void Auditor_and_security_cannot_be_group_members_RN103()
    {
        Assert.Throws<DomainException>(() => SegregationOfDuties.EnsureCanBeGroupMember([SystemRoles.Security], true));
        Assert.Throws<DomainException>(() => SegregationOfDuties.EnsureCanBeGroupMember([SystemRoles.Custodian], false));
        SegregationOfDuties.EnsureCanBeGroupMember([SystemRoles.Administrator], true);
    }

    [Fact]
    public void Security_cannot_own_objects() =>
        Assert.Throws<DomainException>(() => SegregationOfDuties.EnsureCanOwn([SystemRoles.Security], true));
}

using PlatformVault.Domain.Common;
using PlatformVault.Domain.Objects;

namespace PlatformVault.Domain.Access;

/// <summary>Regla de aprobación aplicable a un objeto.</summary>
public sealed record ApprovalRule(ApproverKind ApproverKind, TimeSpan MaxDuration);

/// <summary>
/// Políticas por defecto de RN-045. En la iteración 1A no son configurables (los flujos multinivel
/// configurables pertenecen a la iteración 1B, IMP-24).
/// </summary>
public static class AccessPolicy
{
    public static readonly TimeSpan CriticalOrRestrictedMax = TimeSpan.FromHours(4);
    public static readonly TimeSpan ConfidentialMax = TimeSpan.FromHours(8);
    public static readonly TimeSpan PendingLifetime = TimeSpan.FromHours(72);

    public static ApprovalRule Resolve(Criticality criticality, Sensitivity sensitivity)
    {
        if (criticality == Criticality.Critical)
            return new ApprovalRule(ApproverKind.Security, CriticalOrRestrictedMax);
        return sensitivity switch
        {
            Sensitivity.Restricted => new ApprovalRule(ApproverKind.GroupPeer, CriticalOrRestrictedMax),
            Sensitivity.Confidential => new ApprovalRule(ApproverKind.Owner, ConfidentialMax),
            _ => throw new DomainException(DomainErrors.ActionNotApplicable, "Los objetos Públicos o Internos no custodian valores que revelar (RN-045 d)."),
        };
    }

    /// <summary>Acción que corresponde a la forma del valor custodiado.</summary>
    public static AccessAction ActionFor(PayloadKind kind) => kind switch
    {
        PayloadKind.Text => AccessAction.Reveal,
        PayloadKind.Pkcs12 => AccessAction.DownloadPrivateKey,
        PayloadKind.KeyMaterial => AccessAction.DownloadKeyMaterial,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}

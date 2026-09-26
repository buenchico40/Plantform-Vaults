namespace PlatformVault.Domain.Common;

/// <summary>Códigos estables de error de negocio (se exponen en <c>ProblemDetails.code</c>).</summary>
public static class DomainErrors
{
    public const string RuleViolation = "RULE_VIOLATION";
    public const string InvalidTransition = "INVALID_STATE_TRANSITION";
    public const string ReasonRequired = "REASON_REQUIRED";
    public const string ExpirationRequired = "EXPIRATION_REQUIRED";
    public const string PayloadNotAllowed = "PAYLOAD_NOT_ALLOWED";
    public const string SensitivityTooLow = "SENSITIVITY_TOO_LOW";
    public const string CardDataDetected = "CARD_DATA_DETECTED";
    public const string OwnersRequired = "OWNERS_REQUIRED";
    public const string GroupRequirement = "GROUP_REQUIREMENT_NOT_MET";
    public const string ObjectNotActive = "OBJECT_NOT_ACTIVE";
    public const string JustificationTooShort = "JUSTIFICATION_TOO_SHORT";
    public const string DurationExceeded = "DURATION_EXCEEDED";
    public const string ActionNotApplicable = "ACTION_NOT_APPLICABLE";
    public const string SelfApproval = "SELF_APPROVAL_FORBIDDEN";
    public const string CommentRequired = "COMMENT_REQUIRED";
    public const string RoleConflict = "ROLE_CONFLICT";
    public const string SelfAssignment = "SELF_ASSIGNMENT_FORBIDDEN";
    public const string MemberNotAllowed = "MEMBER_NOT_ALLOWED";
    public const string InvalidThresholds = "INVALID_THRESHOLDS";
    public const string InvalidValue = "INVALID_VALUE";
}

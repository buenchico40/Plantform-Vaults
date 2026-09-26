-- PlatformVault · Detalle de solicitud (US-032)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_AccessRequest_GetById @RequestId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;
    SELECT r.RequestId, r.Code, r.RequesterId, ru.DisplayName AS RequesterName, r.ObjectId, o.Code AS ObjectCode, o.Name AS ObjectName,
           o.Criticality, o.Sensitivity, r.Action, r.Justification, r.RequestedStartUtc, r.DurationMinutes, r.ApproverKind, r.State,
           r.CreatedAtUtc, r.PendingExpiresAtUtc, r.DecidedAtUtc
    FROM app.AccessRequest AS r
    JOIN app.ManagedObject AS o ON o.ObjectId = r.ObjectId
    JOIN [identity].[User] AS ru ON ru.UserId = r.RequesterId
    WHERE r.RequestId = @RequestId;

    SELECT d.ApproverId, u.DisplayName AS ApproverName, d.Decision, d.Comment, d.DecidedAtUtc
    FROM app.ApprovalDecision AS d JOIN [identity].[User] AS u ON u.UserId = d.ApproverId
    WHERE d.RequestId = @RequestId;
END
GO

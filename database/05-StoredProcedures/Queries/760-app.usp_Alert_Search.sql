-- PlatformVault · Alertas visibles para el usuario (US-022, US-024)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_Alert_Search
    @ViewerUserId uniqueidentifier, @HasGlobalScope bit, @CustodianAreaId uniqueidentifier = NULL, @State varchar(15) = NULL, @Severity varchar(10) = NULL, @OnlyOpen bit = 1, @Offset int = 0, @PageSize int = 50,
    @ObjectId uniqueidentifier = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT al.AlertId, al.ObjectId, o.Code AS ObjectCode, o.Name AS ObjectName, o.ObjectType, o.ExpirationDate,
           al.AlertKey, al.Kind, al.ThresholdDays, al.Severity, al.State, al.EscalationLevel, al.CreatedAtUtc, al.LastEscalatedAtUtc,
           al.AcknowledgedBy, al.AcknowledgedAtUtc, al.AcknowledgeComment, al.ResolvedAtUtc, al.ResolutionReason,
           COUNT(*) OVER () AS TotalCount
    FROM app.Alert AS al
    JOIN app.ManagedObject AS o ON o.ObjectId = al.ObjectId
    JOIN app.ufn_VisibleObjects(@ViewerUserId, @HasGlobalScope, @CustodianAreaId) AS v ON v.ObjectId = al.ObjectId
    WHERE (@State IS NULL OR al.State = @State)
      AND (@Severity IS NULL OR al.Severity = @Severity)
      AND (@OnlyOpen = 0 OR al.State <> 'Resolved')
      AND (@ObjectId IS NULL OR al.ObjectId = @ObjectId)
    ORDER BY CASE al.Severity WHEN 'Critical' THEN 1 WHEN 'High' THEN 2 WHEN 'Medium' THEN 3 ELSE 4 END, al.CreatedAtUtc DESC, al.AlertId
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

-- PlatformVault · Historial de versiones (US-011)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_ObjectVersion_ListByObject @ObjectId uniqueidentifier, @ViewerUserId uniqueidentifier, @HasGlobalScope bit, @CustodianAreaId uniqueidentifier = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ov.ObjectId, ov.VersionNumber, ov.ChangedBy, u.DisplayName AS ChangedByName, ov.ChangedAtUtc, ov.Reason, ov.ChangedFieldsJson
    FROM app.ObjectVersion AS ov
    JOIN app.ufn_VisibleObjects(@ViewerUserId, @HasGlobalScope, @CustodianAreaId) AS v ON v.ObjectId = ov.ObjectId
    LEFT JOIN [identity].[User] AS u ON u.UserId = ov.ChangedBy
    WHERE ov.ObjectId = @ObjectId
    ORDER BY ov.VersionNumber DESC;
END
GO

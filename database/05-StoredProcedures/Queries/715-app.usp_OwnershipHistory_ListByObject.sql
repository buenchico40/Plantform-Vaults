-- PlatformVault · Historial de propietarios (US-013, RN-089)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_OwnershipHistory_ListByObject @ObjectId uniqueidentifier, @ViewerUserId uniqueidentifier, @HasGlobalScope bit
AS
BEGIN
    SET NOCOUNT ON;
    SELECT h.OwnershipHistoryId, h.OwnerRole, h.PreviousUserId, pu.DisplayName AS PreviousUserName, h.NewUserId, nu.DisplayName AS NewUserName,
           h.ChangedBy, cu.DisplayName AS ChangedByName, h.ChangedAtUtc, h.Reason
    FROM app.OwnershipHistory AS h
    JOIN app.ufn_VisibleObjects(@ViewerUserId, @HasGlobalScope) AS v ON v.ObjectId = h.ObjectId
    LEFT JOIN [identity].[User] AS pu ON pu.UserId = h.PreviousUserId
    LEFT JOIN [identity].[User] AS nu ON nu.UserId = h.NewUserId
    LEFT JOIN [identity].[User] AS cu ON cu.UserId = h.ChangedBy
    WHERE h.ObjectId = @ObjectId
    ORDER BY h.ChangedAtUtc DESC, h.OwnershipHistoryId DESC;
END
GO

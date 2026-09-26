-- PlatformVault · Accesos temporales del usuario (US-034)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_TemporaryAccess_ListByUser @UserId uniqueidentifier, @OnlyCurrent bit = 1
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ta.AccessId, ta.RequestId, ta.ObjectId, o.Code AS ObjectCode, o.Name AS ObjectName, ta.Action, ta.StartUtc, ta.EndUtc,
           ta.State, ta.RevokedAtUtc, ta.RevokeReason
    FROM app.TemporaryAccess AS ta JOIN app.ManagedObject AS o ON o.ObjectId = ta.ObjectId
    WHERE ta.BeneficiaryId = @UserId AND (@OnlyCurrent = 0 OR ta.State IN ('Scheduled', 'Active'))
    ORDER BY ta.StartUtc DESC;
END
GO

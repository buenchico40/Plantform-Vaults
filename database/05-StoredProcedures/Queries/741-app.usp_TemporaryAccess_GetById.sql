-- PlatformVault · Detalle de acceso temporal
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_TemporaryAccess_GetById @AccessId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ta.AccessId, ta.RequestId, ta.ObjectId, ta.BeneficiaryId, ta.Action, ta.StartUtc, ta.EndUtc, ta.State,
           ta.RevokedAtUtc, ta.RevokedBy, ta.RevokeReason
    FROM app.TemporaryAccess AS ta WHERE ta.AccessId = @AccessId;
END
GO

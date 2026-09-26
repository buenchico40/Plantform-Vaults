-- PlatformVault · Revocación de accesos al desactivar un usuario (RN-031)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_TemporaryAccess_RevokeAllForUser
    @UserId uniqueidentifier, @RevokedBy uniqueidentifier, @Reason nvarchar(300), @NowUtc datetime2(3)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    UPDATE app.TemporaryAccess SET State = 'Revoked', RevokedAtUtc = @NowUtc, RevokedBy = @RevokedBy, RevokeReason = @Reason
    WHERE BeneficiaryId = @UserId AND State IN ('Scheduled', 'Active');
    DECLARE @Revoked int = @@ROWCOUNT;
    UPDATE app.AccessRequest SET State = 'Cancelled', DecidedAtUtc = @NowUtc WHERE RequesterId = @UserId AND State = 'Pending';
    SELECT @Revoked AS RevokedAccesses;
END
GO

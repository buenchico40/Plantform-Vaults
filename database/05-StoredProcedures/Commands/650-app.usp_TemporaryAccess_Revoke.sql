-- PlatformVault · Revocación manual de acceso temporal (US-035, RN-058)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_TemporaryAccess_Revoke
    @AccessId uniqueidentifier, @RevokedBy uniqueidentifier, @Reason nvarchar(300), @NowUtc datetime2(3)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE app.TemporaryAccess SET State = 'Revoked', RevokedAtUtc = @NowUtc, RevokedBy = @RevokedBy, RevokeReason = @Reason
    WHERE AccessId = @AccessId AND State IN ('Scheduled', 'Active');
    IF @@ROWCOUNT = 0 THROW 51003, N'INVALID_STATE|access_not_active', 1;
END
GO

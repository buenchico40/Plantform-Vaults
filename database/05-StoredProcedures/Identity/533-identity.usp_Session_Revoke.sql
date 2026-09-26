-- PlatformVault · Cierre de sesión
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE [identity].usp_Session_Revoke @SessionId uniqueidentifier, @Reason nvarchar(100), @NowUtc datetime2(3)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE [identity].[Session] SET RevokedAtUtc = @NowUtc, RevokeReason = @Reason WHERE SessionId = @SessionId AND RevokedAtUtc IS NULL;
END
GO

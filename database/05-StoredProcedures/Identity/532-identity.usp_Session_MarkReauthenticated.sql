-- PlatformVault · Registro de re-autenticación con contraseña (IMP-29)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE [identity].usp_Session_MarkReauthenticated @SessionId uniqueidentifier, @NowUtc datetime2(3)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE [identity].[Session] SET LastReauthUtc = @NowUtc WHERE SessionId = @SessionId AND RevokedAtUtc IS NULL;
END
GO

-- PlatformVault · Registro del historial de contraseñas (IMP-25)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE [identity].usp_PasswordHistory_Insert @UserId uniqueidentifier, @PasswordHash nvarchar(512)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT [identity].PasswordHistory (UserId, PasswordHash) VALUES (@UserId, @PasswordHash);
END
GO

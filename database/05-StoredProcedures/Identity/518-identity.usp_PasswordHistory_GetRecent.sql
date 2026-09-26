-- PlatformVault · Últimas contraseñas del usuario (IMP-25)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE [identity].usp_PasswordHistory_GetRecent @UserId uniqueidentifier, @Count int = 4
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@Count) PasswordHash FROM [identity].PasswordHistory WHERE UserId = @UserId ORDER BY CreatedAtUtc DESC, PasswordHistoryId DESC;
END
GO

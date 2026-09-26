-- PlatformVault · Consulta de usuario por correo normalizado
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE [identity].usp_User_GetByNormalizedEmail @NormalizedEmail nvarchar(256)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) u.UserId, u.UserName, u.NormalizedUserName, u.Email, u.NormalizedEmail, u.DisplayName, u.PasswordHash,
           u.SecurityStamp, u.ConcurrencyStamp, u.LockoutEnabled, u.LockoutEndUtc, u.AccessFailedCount, u.IsActive,
           u.AreaId, u.ManagerUserId, u.PasswordChangedAtUtc, u.MustChangePassword, u.CreatedAtUtc FROM [identity].[User] AS u WHERE u.NormalizedEmail = @NormalizedEmail ORDER BY u.CreatedAtUtc;
END
GO

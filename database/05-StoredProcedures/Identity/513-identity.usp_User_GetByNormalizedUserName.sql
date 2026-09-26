-- PlatformVault · Consulta de usuario por nombre normalizado
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE [identity].usp_User_GetByNormalizedUserName @NormalizedUserName nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT u.UserId, u.UserName, u.NormalizedUserName, u.Email, u.NormalizedEmail, u.DisplayName, u.PasswordHash,
           u.SecurityStamp, u.ConcurrencyStamp, u.LockoutEnabled, u.LockoutEndUtc, u.AccessFailedCount, u.IsActive,
           u.AreaId, u.ManagerUserId, u.PasswordChangedAtUtc, u.MustChangePassword, u.CreatedAtUtc FROM [identity].[User] AS u WHERE u.NormalizedUserName = @NormalizedUserName;
END
GO

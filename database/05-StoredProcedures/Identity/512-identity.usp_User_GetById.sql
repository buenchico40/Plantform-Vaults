-- PlatformVault · Consulta de usuario por identificador
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE [identity].usp_User_GetById @UserId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;
    SELECT u.UserId, u.UserName, u.NormalizedUserName, u.Email, u.NormalizedEmail, u.DisplayName, u.PasswordHash,
           u.SecurityStamp, u.ConcurrencyStamp, u.LockoutEnabled, u.LockoutEndUtc, u.AccessFailedCount, u.IsActive,
           u.AreaId, u.ManagerUserId, u.PasswordChangedAtUtc, u.MustChangePassword, u.CreatedAtUtc FROM [identity].[User] AS u WHERE u.UserId = @UserId;
END
GO

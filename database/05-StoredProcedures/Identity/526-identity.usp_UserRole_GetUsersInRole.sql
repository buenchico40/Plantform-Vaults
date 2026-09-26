-- PlatformVault · Usuarios de un rol
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE [identity].usp_UserRole_GetUsersInRole @NormalizedRoleName nvarchar(50)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT u.UserId, u.UserName, u.NormalizedUserName, u.Email, u.NormalizedEmail, u.DisplayName, u.PasswordHash,
           u.SecurityStamp, u.ConcurrencyStamp, u.LockoutEnabled, u.LockoutEndUtc, u.AccessFailedCount, u.IsActive,
           u.AreaId, u.ManagerUserId, u.PasswordChangedAtUtc, u.MustChangePassword, u.CreatedAtUtc
    FROM [identity].[User] AS u
    JOIN [identity].UserRole AS ur ON ur.UserId = u.UserId
    JOIN [identity].[Role] AS r ON r.RoleId = ur.RoleId
    WHERE r.NormalizedName = @NormalizedRoleName;
END
GO

-- PlatformVault · Roles de un usuario
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE [identity].usp_UserRole_GetRoleNames @UserId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;
    SELECT r.Name FROM [identity].UserRole AS ur JOIN [identity].[Role] AS r ON r.RoleId = ur.RoleId WHERE ur.UserId = @UserId ORDER BY r.Name;
END
GO

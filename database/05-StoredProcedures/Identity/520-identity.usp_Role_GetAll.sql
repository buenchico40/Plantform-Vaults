-- PlatformVault · Roles de sistema
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE [identity].usp_Role_GetAll
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RoleId, Name, NormalizedName, IsAssignable, IsExclusive, ConcurrencyStamp FROM [identity].[Role] ORDER BY Name;
END
GO

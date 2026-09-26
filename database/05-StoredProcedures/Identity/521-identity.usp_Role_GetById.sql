-- PlatformVault · Rol por identificador
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE [identity].usp_Role_GetById @RoleId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RoleId, Name, NormalizedName, IsAssignable, IsExclusive, ConcurrencyStamp FROM [identity].[Role] WHERE RoleId = @RoleId;
END
GO

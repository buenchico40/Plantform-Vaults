-- PlatformVault · Rol por nombre normalizado
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE [identity].usp_Role_GetByNormalizedName @NormalizedName nvarchar(50)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RoleId, Name, NormalizedName, IsAssignable, IsExclusive, ConcurrencyStamp FROM [identity].[Role] WHERE NormalizedName = @NormalizedName;
END
GO

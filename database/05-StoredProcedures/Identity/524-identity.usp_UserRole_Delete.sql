-- PlatformVault · Revocación de rol
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE [identity].usp_UserRole_Delete @UserId uniqueidentifier, @NormalizedRoleName nvarchar(50)
AS
BEGIN
    SET NOCOUNT ON;
    DELETE ur FROM [identity].UserRole AS ur JOIN [identity].[Role] AS r ON r.RoleId = ur.RoleId
    WHERE ur.UserId = @UserId AND r.NormalizedName = @NormalizedRoleName;
END
GO

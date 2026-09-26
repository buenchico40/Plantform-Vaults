-- PlatformVault · Asignación de rol
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE [identity].usp_UserRole_Insert @UserId uniqueidentifier, @NormalizedRoleName nvarchar(50), @AssignedBy uniqueidentifier = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    DECLARE @RoleId uniqueidentifier = (SELECT RoleId FROM [identity].[Role] WHERE NormalizedName = @NormalizedRoleName);
    IF @RoleId IS NULL THROW 51002, N'NOT_FOUND|role', 1;
    IF NOT EXISTS (SELECT 1 FROM [identity].UserRole WHERE UserId = @UserId AND RoleId = @RoleId)
        INSERT [identity].UserRole (UserId, RoleId, AssignedBy) VALUES (@UserId, @RoleId, @AssignedBy);
END
GO

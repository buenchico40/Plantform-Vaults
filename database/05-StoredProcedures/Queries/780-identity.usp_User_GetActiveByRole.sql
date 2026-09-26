-- PlatformVault · Usuarios activos con un rol (notificaciones a Seguridad, RN-117)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE [identity].usp_User_GetActiveByRole @NormalizedRoleName nvarchar(50)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT u.UserId, u.DisplayName, u.Email
    FROM [identity].[User] AS u
    JOIN [identity].UserRole AS ur ON ur.UserId = u.UserId
    JOIN [identity].[Role] AS r ON r.RoleId = ur.RoleId
    WHERE r.NormalizedName = @NormalizedRoleName AND u.IsActive = 1;
END
GO

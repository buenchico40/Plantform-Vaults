-- PlatformVault · Datos de contacto de un conjunto de usuarios activos
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE [identity].usp_User_GetContacts @UserIds app.GuidList READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SELECT u.UserId, u.UserName, u.DisplayName, u.Email
    FROM [identity].[User] AS u JOIN @UserIds AS i ON i.Id = u.UserId
    WHERE u.IsActive = 1;
END
GO

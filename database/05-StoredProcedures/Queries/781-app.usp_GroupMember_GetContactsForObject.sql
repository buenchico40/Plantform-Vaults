-- PlatformVault · Miembros de los grupos del objeto para notificaciones (RN-116)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_GroupMember_GetContactsForObject @ObjectId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DISTINCT u.UserId, u.DisplayName, u.Email
    FROM app.ObjectGroup AS og
    JOIN app.SecurityGroup AS g ON g.GroupId = og.GroupId AND g.IsActive = 1
    JOIN app.GroupMember AS gm ON gm.GroupId = og.GroupId
    JOIN [identity].[User] AS u ON u.UserId = gm.UserId AND u.IsActive = 1
    WHERE og.ObjectId = @ObjectId;
END
GO

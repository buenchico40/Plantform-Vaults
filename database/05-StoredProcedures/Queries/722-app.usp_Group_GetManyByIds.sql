-- PlatformVault · Validación de grupos al asignarlos a un objeto (US-028)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_Group_GetManyByIds @GroupIds app.GuidList READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SELECT g.GroupId, g.Code, g.Name, g.IsActive,
           (SELECT COUNT(*) FROM app.GroupMember AS gm JOIN [identity].[User] AS u ON u.UserId = gm.UserId AND u.IsActive = 1
             WHERE gm.GroupId = g.GroupId) AS ActiveMemberCount
    FROM app.SecurityGroup AS g JOIN @GroupIds AS i ON i.Id = g.GroupId;
END
GO

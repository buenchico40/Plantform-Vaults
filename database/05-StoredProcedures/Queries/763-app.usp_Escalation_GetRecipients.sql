-- PlatformVault · Destinatarios por nivel de escalamiento (RN-068)
SET NOCOUNT ON;
GO
-- IMP-62: N1 propietario · N2 jefe del propietario y Responsables de los grupos activos del objeto · N3 Seguridad.
CREATE OR ALTER PROCEDURE app.usp_Escalation_GetRecipients @ObjectId uniqueidentifier, @Level tinyint
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Owner uniqueidentifier;
    SELECT @Owner = OwnerId FROM app.ManagedObject WHERE ObjectId = @ObjectId;

    SELECT DISTINCT u.UserId, u.DisplayName, u.Email
    FROM [identity].[User] AS u
    WHERE u.IsActive = 1 AND (
          (@Level = 1 AND u.UserId = @Owner)
       OR (@Level = 2 AND (u.UserId = (SELECT ManagerUserId FROM [identity].[User] WHERE UserId = @Owner)
              OR EXISTS (SELECT 1 FROM app.ObjectGroup AS og
                         JOIN app.SecurityGroup AS g ON g.GroupId = og.GroupId AND g.IsActive = 1
                         JOIN app.GroupMember AS gm ON gm.GroupId = og.GroupId AND gm.IsResponsible = 1
                         WHERE og.ObjectId = @ObjectId AND gm.UserId = u.UserId)))
       OR (@Level = 3 AND EXISTS (SELECT 1 FROM [identity].UserRole AS ur JOIN [identity].[Role] AS r ON r.RoleId = ur.RoleId
                                  WHERE ur.UserId = u.UserId AND r.NormalizedName = N'SEGURIDAD')));
END
GO

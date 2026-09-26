-- PlatformVault · Destinatarios por nivel de escalamiento (RN-068)
SET NOCOUNT ON;
GO
-- N1 propietario técnico · N2 propietario funcional · N3 jefe del propietario funcional y custodios del área · N4 Seguridad.
CREATE OR ALTER PROCEDURE app.usp_Escalation_GetRecipients @ObjectId uniqueidentifier, @Level tinyint
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Tech uniqueidentifier, @Func uniqueidentifier, @AreaId uniqueidentifier;
    SELECT @Tech = TechnicalOwnerId, @Func = FunctionalOwnerId, @AreaId = AreaId FROM app.ManagedObject WHERE ObjectId = @ObjectId;

    SELECT DISTINCT u.UserId, u.DisplayName, u.Email
    FROM [identity].[User] AS u
    WHERE u.IsActive = 1 AND (
          (@Level = 1 AND u.UserId = @Tech)
       OR (@Level = 2 AND u.UserId = @Func)
       OR (@Level = 3 AND (u.UserId = (SELECT ManagerUserId FROM [identity].[User] WHERE UserId = @Func)
              OR (u.AreaId = @AreaId AND EXISTS (SELECT 1 FROM [identity].UserRole AS ur JOIN [identity].[Role] AS r ON r.RoleId = ur.RoleId
                                                 WHERE ur.UserId = u.UserId AND r.NormalizedName = N'CUSTODIO'))))
       OR (@Level = 4 AND EXISTS (SELECT 1 FROM [identity].UserRole AS ur JOIN [identity].[Role] AS r ON r.RoleId = ur.RoleId
                                  WHERE ur.UserId = u.UserId AND r.NormalizedName = N'SEGURIDAD')));
END
GO

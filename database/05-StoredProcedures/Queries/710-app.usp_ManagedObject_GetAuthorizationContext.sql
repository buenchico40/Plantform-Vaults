-- PlatformVault · Contexto para la autorización por objeto (IMP-06, protección IDOR/BOLA)
SET NOCOUNT ON;
GO
-- Sin filtro de visibilidad: lo usa el servicio de autorización para decidir. Nunca se devuelve tal cual al cliente.
CREATE OR ALTER PROCEDURE app.usp_ManagedObject_GetAuthorizationContext
    @ObjectId uniqueidentifier, @ViewerUserId uniqueidentifier, @NowUtc datetime2(3)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT o.ObjectId, o.Code, o.ObjectType, o.Criticality, o.Sensitivity, o.LifecycleState, o.CustodyMode, o.HasPayload,
           o.AreaId, o.FunctionalOwnerId, o.TechnicalOwnerId, o.CreatedBy, o.CurrentVersion, o.RowVer
    FROM app.ManagedObject AS o WHERE o.ObjectId = @ObjectId;

    SELECT g.GroupId, g.Code, g.Name, g.IsActive,
           (SELECT COUNT(*) FROM app.GroupMember AS gm JOIN [identity].[User] AS u ON u.UserId = gm.UserId AND u.IsActive = 1
             WHERE gm.GroupId = g.GroupId) AS ActiveMemberCount,
           CAST(CASE WHEN EXISTS (SELECT 1 FROM app.GroupMember AS gm WHERE gm.GroupId = g.GroupId AND gm.UserId = @ViewerUserId)
                     THEN 1 ELSE 0 END AS bit) AS ViewerIsMember
    FROM app.ObjectGroup AS og JOIN app.SecurityGroup AS g ON g.GroupId = og.GroupId
    WHERE og.ObjectId = @ObjectId;

    SELECT ta.AccessId, ta.Action, ta.StartUtc, ta.EndUtc
    FROM app.TemporaryAccess AS ta
    WHERE ta.ObjectId = @ObjectId AND ta.BeneficiaryId = @ViewerUserId AND ta.State = 'Active'
      AND ta.StartUtc <= @NowUtc AND ta.EndUtc > @NowUtc;
END
GO

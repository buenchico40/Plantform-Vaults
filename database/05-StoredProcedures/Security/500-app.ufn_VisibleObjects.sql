-- PlatformVault · Predicado de visibilidad de objetos (RN-010, defensa en profundidad)
SET NOCOUNT ON;
GO
-- La decisión de ámbito (global o por área de custodio) la toma la capa Application y la pasa como parámetro.
-- Esta función la aplica en la base de datos para que ninguna consulta devuelva objetos fuera del ámbito.
CREATE OR ALTER FUNCTION app.ufn_VisibleObjects
(
    @ViewerUserId    uniqueidentifier,
    @HasGlobalScope  bit,
    @CustodianAreaId uniqueidentifier
)
RETURNS TABLE
AS
RETURN
    SELECT o.ObjectId
    FROM app.ManagedObject AS o
    WHERE @HasGlobalScope = 1
       OR o.FunctionalOwnerId = @ViewerUserId
       OR o.TechnicalOwnerId = @ViewerUserId
       OR (@CustodianAreaId IS NOT NULL AND o.AreaId = @CustodianAreaId)
       OR EXISTS (
            SELECT 1
            FROM app.ObjectGroup AS og
            JOIN app.SecurityGroup AS g ON g.GroupId = og.GroupId AND g.IsActive = 1
            JOIN app.GroupMember AS gm ON gm.GroupId = og.GroupId
            WHERE og.ObjectId = o.ObjectId AND gm.UserId = @ViewerUserId);
GO

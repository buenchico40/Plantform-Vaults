-- PlatformVault · Predicado de visibilidad de objetos (RN-010, defensa en profundidad)
SET NOCOUNT ON;
GO
-- La decisión de ámbito global la toma la capa Application y la pasa como parámetro.
-- Esta función la aplica en la base de datos para que ninguna consulta devuelva objetos fuera del ámbito.
-- IMP-61: sin ámbito global se ven los objetos propios (propietario), los registrados por el usuario
-- y los asignados a un grupo activo del que es miembro. Ya no hay visibilidad por área.
CREATE OR ALTER FUNCTION app.ufn_VisibleObjects
(
    @ViewerUserId    uniqueidentifier,
    @HasGlobalScope  bit
)
RETURNS TABLE
AS
RETURN
    SELECT o.ObjectId
    FROM app.ManagedObject AS o
    WHERE @HasGlobalScope = 1
       OR o.FunctionalOwnerId = @ViewerUserId
       OR o.TechnicalOwnerId = @ViewerUserId
       OR o.CreatedBy = @ViewerUserId
       OR EXISTS (
            SELECT 1
            FROM app.ObjectGroup AS og
            JOIN app.SecurityGroup AS g ON g.GroupId = og.GroupId AND g.IsActive = 1
            JOIN app.GroupMember AS gm ON gm.GroupId = og.GroupId
            WHERE og.ObjectId = o.ObjectId AND gm.UserId = @ViewerUserId);
GO

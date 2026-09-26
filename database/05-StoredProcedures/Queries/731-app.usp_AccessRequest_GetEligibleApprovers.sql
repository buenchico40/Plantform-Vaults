-- PlatformVault · Aprobadores elegibles según el tipo de aprobación (RN-106, RN-122, RN-051)
SET NOCOUNT ON;
GO
-- El solicitante nunca es elegible (sin autoaprobación). Solo usuarios activos.
CREATE OR ALTER PROCEDURE app.usp_AccessRequest_GetEligibleApprovers
    @ObjectId uniqueidentifier, @RequesterId uniqueidentifier, @ApproverKind varchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DISTINCT u.UserId, u.DisplayName, u.Email
    FROM [identity].[User] AS u
    WHERE u.IsActive = 1 AND u.UserId <> @RequesterId
      AND (
            (@ApproverKind = 'Security' AND EXISTS (
                SELECT 1 FROM [identity].UserRole AS ur JOIN [identity].[Role] AS r ON r.RoleId = ur.RoleId
                WHERE ur.UserId = u.UserId AND r.NormalizedName = N'SEGURIDAD'))
         OR (@ApproverKind = 'GroupPeer' AND EXISTS (
                SELECT 1 FROM app.ObjectGroup AS og
                JOIN app.SecurityGroup AS g ON g.GroupId = og.GroupId AND g.IsActive = 1
                JOIN app.GroupMember AS peer ON peer.GroupId = og.GroupId AND peer.UserId = u.UserId
                JOIN app.GroupMember AS req ON req.GroupId = og.GroupId AND req.UserId = @RequesterId
                WHERE og.ObjectId = @ObjectId))
         OR (@ApproverKind = 'Owner' AND EXISTS (
                SELECT 1 FROM app.ManagedObject AS o
                WHERE o.ObjectId = @ObjectId AND o.OwnerId = u.UserId))
          );
END
GO

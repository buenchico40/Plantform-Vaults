-- PlatformVault · Solicitudes: propias, pendientes para el usuario o todas (US-032, US-033)
SET NOCOUNT ON;
GO
-- @Scope: Mine | PendingForMe | All. "All" solo lo invoca la aplicación para roles con ámbito global.
CREATE OR ALTER PROCEDURE app.usp_AccessRequest_Search
    @Scope varchar(15), @ViewerUserId uniqueidentifier, @ViewerIsSecurity bit, @State varchar(15) = NULL,
    @Offset int = 0, @PageSize int = 50
AS
BEGIN
    SET NOCOUNT ON;
    SELECT r.RequestId, r.Code, r.RequesterId, ru.DisplayName AS RequesterName, r.ObjectId, o.Code AS ObjectCode, o.Name AS ObjectName,
           o.Criticality, o.Sensitivity, r.Action, r.RequestedStartUtc, r.DurationMinutes, r.ApproverKind, r.State,
           r.CreatedAtUtc, r.PendingExpiresAtUtc, r.DecidedAtUtc, COUNT(*) OVER () AS TotalCount
    FROM app.AccessRequest AS r
    JOIN app.ManagedObject AS o ON o.ObjectId = r.ObjectId
    JOIN [identity].[User] AS ru ON ru.UserId = r.RequesterId
    WHERE (@State IS NULL OR r.State = @State)
      AND (
            (@Scope = 'Mine' AND r.RequesterId = @ViewerUserId)
         OR (@Scope = 'All')
         OR (@Scope = 'PendingForMe' AND r.State = 'Pending' AND r.RequesterId <> @ViewerUserId AND (
                (r.ApproverKind = 'Security' AND @ViewerIsSecurity = 1)
             OR (r.ApproverKind = 'Owner' AND (o.FunctionalOwnerId = @ViewerUserId OR o.TechnicalOwnerId = @ViewerUserId))
             OR (r.ApproverKind = 'GroupPeer' AND EXISTS (
                    SELECT 1 FROM app.ObjectGroup AS og
                    JOIN app.SecurityGroup AS g ON g.GroupId = og.GroupId AND g.IsActive = 1
                    JOIN app.GroupMember AS peer ON peer.GroupId = og.GroupId AND peer.UserId = @ViewerUserId
                    JOIN app.GroupMember AS req ON req.GroupId = og.GroupId AND req.UserId = r.RequesterId
                    WHERE og.ObjectId = r.ObjectId))))
          )
    ORDER BY r.CreatedAtUtc DESC, r.RequestId
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

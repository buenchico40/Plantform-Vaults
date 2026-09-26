-- PlatformVault · Tablero principal filtrado por visibilidad (US-038)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE report.usp_Dashboard_Get
    @ViewerUserId uniqueidentifier, @HasGlobalScope bit, @CustodianAreaId uniqueidentifier = NULL, @NowUtc datetime2(3), @ExpiringSoonDays int = 30
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Visible TABLE (ObjectId uniqueidentifier PRIMARY KEY, ObjectType varchar(20), Criticality varchar(10), Sensitivity varchar(15),
        LifecycleState varchar(15), ExpirationStatus varchar(15), WithoutOwner bit);
    INSERT @Visible
    SELECT o.ObjectId, o.ObjectType, o.Criticality, o.Sensitivity, o.LifecycleState,
           CASE WHEN o.ExpirationDate IS NULL THEN 'NoExpiration'
                WHEN o.ExpirationDate <= @NowUtc THEN 'Expired'
                WHEN o.ExpirationDate <= DATEADD(DAY, @ExpiringSoonDays, @NowUtc) THEN 'ExpiringSoon'
                ELSE 'Valid' END,
           CASE WHEN o.FunctionalOwnerId IS NULL OR o.TechnicalOwnerId IS NULL THEN 1 ELSE 0 END
    FROM app.ManagedObject AS o
    JOIN app.ufn_VisibleObjects(@ViewerUserId, @HasGlobalScope, @CustodianAreaId) AS v ON v.ObjectId = o.ObjectId;

    SELECT COUNT(*) AS Total,
           SUM(CASE WHEN LifecycleState = 'Active' THEN 1 ELSE 0 END) AS Active,
           SUM(CASE WHEN ExpirationStatus = 'Expired' AND LifecycleState <> 'Deactivated' THEN 1 ELSE 0 END) AS Expired,
           SUM(CASE WHEN ExpirationStatus = 'ExpiringSoon' AND LifecycleState <> 'Deactivated' THEN 1 ELSE 0 END) AS ExpiringSoon,
           SUM(CASE WHEN WithoutOwner = 1 AND LifecycleState <> 'Deactivated' THEN 1 ELSE 0 END) AS WithoutOwner,
           (SELECT COUNT(*) FROM app.Alert AS al JOIN @Visible AS v2 ON v2.ObjectId = al.ObjectId WHERE al.State <> 'Resolved') AS OpenAlerts,
           (SELECT COUNT(*) FROM app.AccessRequest AS r JOIN @Visible AS v3 ON v3.ObjectId = r.ObjectId WHERE r.State = 'Pending') AS PendingRequests
    FROM @Visible;

    SELECT ObjectType AS [Key], COUNT(*) AS [Count] FROM @Visible GROUP BY ObjectType;
    SELECT Criticality AS [Key], COUNT(*) AS [Count] FROM @Visible GROUP BY Criticality;
    SELECT Sensitivity AS [Key], COUNT(*) AS [Count] FROM @Visible GROUP BY Sensitivity;
    SELECT LifecycleState AS [Key], COUNT(*) AS [Count] FROM @Visible GROUP BY LifecycleState;
    SELECT ExpirationStatus AS [Key], COUNT(*) AS [Count] FROM @Visible WHERE LifecycleState <> 'Deactivated' GROUP BY ExpirationStatus;
END
GO

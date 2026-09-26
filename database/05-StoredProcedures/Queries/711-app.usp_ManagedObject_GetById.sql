-- PlatformVault · Detalle de objeto filtrado por visibilidad (US-008)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_ManagedObject_GetById
    @ObjectId uniqueidentifier, @ViewerUserId uniqueidentifier, @HasGlobalScope bit, @CustodianAreaId uniqueidentifier = NULL, @NowUtc datetime2(3), @ExpiringSoonDays int = 30
AS
BEGIN
    SET NOCOUNT ON;
    SELECT o.ObjectId, o.Code, o.ObjectType, o.Subtype, o.Name, o.Description, o.Criticality, o.Sensitivity, o.Environment,
           o.AreaId, a.Name AS AreaName, o.FunctionalOwnerId, fo.DisplayName AS FunctionalOwnerName,
           o.TechnicalOwnerId, tec.DisplayName AS TechnicalOwnerName, o.LifecycleState, o.CustodyMode, o.HasPayload,
           o.ExpirationDate, o.NoExpirationJustified, o.Thumbprint, o.CurrentVersion, o.CreatedAtUtc, o.CreatedBy,
           o.ModifiedAtUtc, o.ModifiedBy, o.RowVer, o.DetailsJson,
           CASE WHEN o.ExpirationDate IS NULL THEN 'NoExpiration'
                WHEN o.ExpirationDate <= @NowUtc THEN 'Expired'
                WHEN o.ExpirationDate <= DATEADD(DAY, @ExpiringSoonDays, @NowUtc) THEN 'ExpiringSoon'
                ELSE 'Valid' END AS ExpirationStatus
    FROM app.ManagedObject AS o
    JOIN app.ufn_VisibleObjects(@ViewerUserId, @HasGlobalScope, @CustodianAreaId) AS v ON v.ObjectId = o.ObjectId
    JOIN app.Area AS a ON a.AreaId = o.AreaId
    LEFT JOIN [identity].[User] AS fo ON fo.UserId = o.FunctionalOwnerId
    LEFT JOIN [identity].[User] AS tec ON tec.UserId = o.TechnicalOwnerId
    WHERE o.ObjectId = @ObjectId;

    SELECT g.GroupId, g.Code, g.Name, g.IsActive
    FROM app.ObjectGroup AS og
    JOIN app.SecurityGroup AS g ON g.GroupId = og.GroupId
    JOIN app.ufn_VisibleObjects(@ViewerUserId, @HasGlobalScope, @CustodianAreaId) AS v ON v.ObjectId = og.ObjectId
    WHERE og.ObjectId = @ObjectId
    ORDER BY g.Name;
END
GO

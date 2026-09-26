-- PlatformVault · Búsqueda paginada del inventario (US-008, US-009, US-010, RNF-REN-02)
SET NOCOUNT ON;
GO
-- Orden por lista blanca (CASE), sin SQL dinámico.
CREATE OR ALTER PROCEDURE app.usp_ManagedObject_Search
    @ViewerUserId uniqueidentifier, @HasGlobalScope bit, @CustodianAreaId uniqueidentifier = NULL, @NowUtc datetime2(3), @ExpiringSoonDays int = 30,
    @Text nvarchar(200) = NULL, @ObjectType varchar(20) = NULL, @Criticality varchar(10) = NULL, @Sensitivity varchar(15) = NULL,
    @Environment varchar(20) = NULL, @LifecycleState varchar(15) = NULL, @AreaId uniqueidentifier = NULL,
    @OwnerId uniqueidentifier = NULL, @ExpirationStatus varchar(15) = NULL, @WithoutOwner bit = 0,
    @ExpiresBeforeUtc datetime2(0) = NULL, @SortBy varchar(20) = 'Name', @SortDescending bit = 0, @Offset int = 0, @PageSize int = 50,
    @Subtype varchar(50) = NULL, @GroupId uniqueidentifier = NULL, @ExpiresAfterUtc datetime2(0) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Pattern nvarchar(202) = CASE WHEN @Text IS NULL THEN NULL
        ELSE N'%' + REPLACE(REPLACE(REPLACE(@Text, N'[', N'[[]'), N'%', N'[%]'), N'_', N'[_]') + N'%' END;

    WITH Filtered AS (
        SELECT o.ObjectId, o.Code, o.ObjectType, o.Subtype, o.Name, o.Description, o.Criticality, o.Sensitivity, o.Environment,
           o.AreaId, a.Name AS AreaName, o.FunctionalOwnerId, fo.DisplayName AS FunctionalOwnerName,
           o.TechnicalOwnerId, tec.DisplayName AS TechnicalOwnerName, o.LifecycleState, o.CustodyMode, o.HasPayload,
           o.ExpirationDate, o.NoExpirationJustified, o.Thumbprint, o.CurrentVersion, o.CreatedAtUtc, o.CreatedBy,
           o.ModifiedAtUtc, o.ModifiedBy, o.RowVer,
               CASE WHEN o.ExpirationDate IS NULL THEN 'NoExpiration'
                WHEN o.ExpirationDate <= @NowUtc THEN 'Expired'
                WHEN o.ExpirationDate <= DATEADD(DAY, @ExpiringSoonDays, @NowUtc) THEN 'ExpiringSoon'
                ELSE 'Valid' END AS ExpirationStatus
        FROM app.ManagedObject AS o
        JOIN app.ufn_VisibleObjects(@ViewerUserId, @HasGlobalScope, @CustodianAreaId) AS v ON v.ObjectId = o.ObjectId
        JOIN app.Area AS a ON a.AreaId = o.AreaId
    LEFT JOIN [identity].[User] AS fo ON fo.UserId = o.FunctionalOwnerId
    LEFT JOIN [identity].[User] AS tec ON tec.UserId = o.TechnicalOwnerId
        WHERE (@Pattern IS NULL OR o.Name LIKE @Pattern OR o.Code LIKE @Pattern OR o.Subtype LIKE @Pattern)
          AND (@ObjectType IS NULL OR o.ObjectType = @ObjectType)
          AND (@Criticality IS NULL OR o.Criticality = @Criticality)
          AND (@Sensitivity IS NULL OR o.Sensitivity = @Sensitivity)
          AND (@Environment IS NULL OR o.Environment = @Environment)
          AND (@LifecycleState IS NULL OR o.LifecycleState = @LifecycleState)
          AND (@AreaId IS NULL OR o.AreaId = @AreaId)
          AND (@OwnerId IS NULL OR o.FunctionalOwnerId = @OwnerId OR o.TechnicalOwnerId = @OwnerId)
          AND (@WithoutOwner = 0 OR o.FunctionalOwnerId IS NULL OR o.TechnicalOwnerId IS NULL)
          AND (@ExpiresBeforeUtc IS NULL OR o.ExpirationDate <= @ExpiresBeforeUtc)
          AND (@ExpiresAfterUtc IS NULL OR o.ExpirationDate >= @ExpiresAfterUtc)
          AND (@Subtype IS NULL OR o.Subtype = @Subtype)
          AND (@GroupId IS NULL OR EXISTS (SELECT 1 FROM app.ObjectGroup AS og WHERE og.ObjectId = o.ObjectId AND og.GroupId = @GroupId))
    )
    SELECT f.*, COUNT(*) OVER () AS TotalCount
    FROM Filtered AS f
    WHERE (@ExpirationStatus IS NULL OR f.ExpirationStatus = @ExpirationStatus)
    ORDER BY
        CASE WHEN @SortDescending = 0 AND @SortBy = 'Name' THEN f.Name END ASC,
        CASE WHEN @SortDescending = 1 AND @SortBy = 'Name' THEN f.Name END DESC,
        CASE WHEN @SortDescending = 0 AND @SortBy = 'Code' THEN f.Code END ASC,
        CASE WHEN @SortDescending = 1 AND @SortBy = 'Code' THEN f.Code END DESC,
        CASE WHEN @SortDescending = 0 AND @SortBy = 'ExpirationDate' THEN f.ExpirationDate END ASC,
        CASE WHEN @SortDescending = 1 AND @SortBy = 'ExpirationDate' THEN f.ExpirationDate END DESC,
        CASE WHEN @SortDescending = 0 AND @SortBy = 'CreatedAt' THEN f.CreatedAtUtc END ASC,
        CASE WHEN @SortDescending = 1 AND @SortBy = 'CreatedAt' THEN f.CreatedAtUtc END DESC,
        CASE WHEN @SortDescending = 0 AND @SortBy = 'Criticality' THEN
            CASE f.Criticality WHEN 'Critical' THEN 1 WHEN 'High' THEN 2 WHEN 'Medium' THEN 3 ELSE 4 END END ASC,
        CASE WHEN @SortDescending = 1 AND @SortBy = 'Criticality' THEN
            CASE f.Criticality WHEN 'Critical' THEN 1 WHEN 'High' THEN 2 WHEN 'Medium' THEN 3 ELSE 4 END END DESC,
        f.ObjectId
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
    OPTION (RECOMPILE);
END
GO

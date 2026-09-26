-- PlatformVault · Listado paginado de grupos (US-027)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_Group_Search
    @ViewerUserId uniqueidentifier, @HasGlobalScope bit, @Text nvarchar(150) = NULL, @IsActive bit = NULL,
    @Offset int = 0, @PageSize int = 50
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Pattern nvarchar(152) = CASE WHEN @Text IS NULL THEN NULL
        ELSE N'%' + REPLACE(REPLACE(REPLACE(@Text, N'[', N'[[]'), N'%', N'[%]'), N'_', N'[_]') + N'%' END;
    SELECT g.GroupId, g.Code, g.Name, g.Description, g.AreaId, g.IsActive,
           (SELECT COUNT(*) FROM app.GroupMember AS gm WHERE gm.GroupId = g.GroupId) AS MemberCount,
           (SELECT COUNT(*) FROM app.ObjectGroup AS og WHERE og.GroupId = g.GroupId) AS ObjectCount,
           COUNT(*) OVER () AS TotalCount
    FROM app.SecurityGroup AS g
    WHERE (@HasGlobalScope = 1 OR EXISTS (SELECT 1 FROM app.GroupMember AS gm WHERE gm.GroupId = g.GroupId AND gm.UserId = @ViewerUserId))
      AND (@Pattern IS NULL OR g.Name LIKE @Pattern OR g.Code LIKE @Pattern)
      AND (@IsActive IS NULL OR g.IsActive = @IsActive)
    ORDER BY g.Name
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

-- PlatformVault · Búsqueda paginada de usuarios
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE [identity].usp_User_Search
    @Query nvarchar(100) = NULL, @RoleName nvarchar(50) = NULL, @IsActive bit = NULL,
    @PageNumber int = 1, @PageSize int = 25
AS
BEGIN
    SET NOCOUNT ON;
    IF @PageNumber < 1 SET @PageNumber = 1;
    IF @PageSize NOT BETWEEN 1 AND 200 SET @PageSize = 25;
    -- Los comodines del texto se escapan: la búsqueda es literal.
    SET @Query = REPLACE(REPLACE(REPLACE(REPLACE(@Query, N'\', N'\\'), N'%', N'\%'), N'_', N'\_'), N'[', N'\[');
    SELECT u.UserId, u.UserName, u.DisplayName, u.Email, u.IsActive, u.AreaId, a.Name AS AreaName, u.ManagerUserId,
           u.LockoutEndUtc, u.MustChangePassword, u.PasswordChangedAtUtc, u.CreatedAtUtc,
           (SELECT STRING_AGG(r.Name, ',') FROM [identity].UserRole AS ur JOIN [identity].[Role] AS r ON r.RoleId = ur.RoleId
             WHERE ur.UserId = u.UserId) AS Roles,
           COUNT(*) OVER () AS TotalCount
    FROM [identity].[User] AS u
    LEFT JOIN app.Area AS a ON a.AreaId = u.AreaId
    WHERE (@Query IS NULL OR u.UserName LIKE N'%' + @Query + N'%' ESCAPE N'\' OR u.DisplayName LIKE N'%' + @Query + N'%' ESCAPE N'\')
      AND (@IsActive IS NULL OR u.IsActive = @IsActive)
      AND (@RoleName IS NULL OR EXISTS (SELECT 1 FROM [identity].UserRole AS ur JOIN [identity].[Role] AS r ON r.RoleId = ur.RoleId
                                         WHERE ur.UserId = u.UserId AND r.Name = @RoleName))
    ORDER BY u.UserName
    OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY
    OPTION (RECOMPILE);
END
GO

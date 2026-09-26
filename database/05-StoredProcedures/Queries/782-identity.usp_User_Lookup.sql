-- PlatformVault · Búsqueda mínima de usuarios activos para selectores (sin correo ni roles)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE [identity].usp_User_Lookup @Text nvarchar(100) = NULL, @Top int = 20
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Pattern nvarchar(102) = CASE WHEN @Text IS NULL THEN NULL
        ELSE N'%' + REPLACE(REPLACE(REPLACE(@Text, N'[', N'[[]'), N'%', N'[%]'), N'_', N'[_]') + N'%' END;
    SELECT TOP (@Top) u.UserId, u.UserName, u.DisplayName
    FROM [identity].[User] AS u
    WHERE u.IsActive = 1 AND (@Pattern IS NULL OR u.UserName LIKE @Pattern OR u.DisplayName LIKE @Pattern)
    ORDER BY u.DisplayName;
END
GO

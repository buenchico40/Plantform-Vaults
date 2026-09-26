-- PlatformVault · Catálogo de áreas
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_Area_GetAll
AS
BEGIN
    SET NOCOUNT ON;
    SELECT AreaId, Code, Name, IsActive FROM app.Area ORDER BY Name;
END
GO

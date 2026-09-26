-- PlatformVault · Alta de área
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_Area_Insert @AreaId uniqueidentifier, @Code varchar(30), @Name nvarchar(150)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT app.Area (AreaId, Code, Name) VALUES (@AreaId, @Code, @Name);
END
GO

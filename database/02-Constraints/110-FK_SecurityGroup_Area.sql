-- PlatformVault · Restricción app.FK_SecurityGroup_Area
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.FK_SecurityGroup_Area') IS NULL
    ALTER TABLE app.SecurityGroup ADD CONSTRAINT FK_SecurityGroup_Area FOREIGN KEY (AreaId) REFERENCES app.Area (AreaId);
GO

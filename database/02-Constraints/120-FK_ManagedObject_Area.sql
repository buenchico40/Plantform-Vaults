-- PlatformVault · Restricción app.FK_ManagedObject_Area
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.FK_ManagedObject_Area') IS NULL
    ALTER TABLE app.ManagedObject ADD CONSTRAINT FK_ManagedObject_Area FOREIGN KEY (AreaId) REFERENCES app.Area (AreaId);
GO

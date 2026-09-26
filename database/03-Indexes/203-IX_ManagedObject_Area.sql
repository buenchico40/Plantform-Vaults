-- PlatformVault · Índice IX_ManagedObject_Area
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ManagedObject_Area' AND object_id = OBJECT_ID(N'app.ManagedObject'))
    CREATE INDEX IX_ManagedObject_Area ON app.ManagedObject (AreaId);
GO

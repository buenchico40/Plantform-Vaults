-- PlatformVault · Índice IX_ManagedObject_CreatedBy
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

-- IMP-61: el Custodio ve los objetos que registró (app.ufn_VisibleObjects).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ManagedObject_CreatedBy' AND object_id = OBJECT_ID(N'app.ManagedObject'))
    CREATE INDEX IX_ManagedObject_CreatedBy ON app.ManagedObject (CreatedBy);
GO

-- PlatformVault · Índice UX_ManagedObject_Name
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_ManagedObject_Name' AND object_id = OBJECT_ID(N'app.ManagedObject'))
    CREATE UNIQUE INDEX UX_ManagedObject_Name ON app.ManagedObject (ObjectType, Environment, AreaId, Name);
GO

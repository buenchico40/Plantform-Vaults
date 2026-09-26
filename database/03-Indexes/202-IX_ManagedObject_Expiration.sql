-- PlatformVault · Índice IX_ManagedObject_Expiration
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ManagedObject_Expiration' AND object_id = OBJECT_ID(N'app.ManagedObject'))
    CREATE INDEX IX_ManagedObject_Expiration ON app.ManagedObject (ExpirationDate) INCLUDE (LifecycleState, Criticality, ObjectType);
GO

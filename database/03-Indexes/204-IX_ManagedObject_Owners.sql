-- PlatformVault · Índice IX_ManagedObject_Owners
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ManagedObject_Owners' AND object_id = OBJECT_ID(N'app.ManagedObject'))
    CREATE INDEX IX_ManagedObject_Owners ON app.ManagedObject (FunctionalOwnerId, TechnicalOwnerId);
GO

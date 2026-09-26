-- PlatformVault · Índice IX_UsageEvent_Object
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_UsageEvent_Object' AND object_id = OBJECT_ID(N'audit.UsageEvent'))
    CREATE INDEX IX_UsageEvent_Object ON audit.UsageEvent (ObjectId, TimestampUtc DESC);
GO

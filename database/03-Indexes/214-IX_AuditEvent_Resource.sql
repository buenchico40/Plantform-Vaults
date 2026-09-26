-- PlatformVault · Índice IX_AuditEvent_Resource
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditEvent_Resource' AND object_id = OBJECT_ID(N'audit.AuditEvent'))
    CREATE INDEX IX_AuditEvent_Resource ON audit.AuditEvent (ResourceType, ResourceId, TimestampUtc);
GO

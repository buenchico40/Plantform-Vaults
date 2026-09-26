-- PlatformVault · Índice IX_AuditEvent_Timestamp
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditEvent_Timestamp' AND object_id = OBJECT_ID(N'audit.AuditEvent'))
    CREATE INDEX IX_AuditEvent_Timestamp ON audit.AuditEvent (TimestampUtc) INCLUDE (Action, ActorId, Result);
GO

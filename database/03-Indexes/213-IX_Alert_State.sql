-- PlatformVault · Índice IX_Alert_State
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Alert_State' AND object_id = OBJECT_ID(N'app.Alert'))
    CREATE INDEX IX_Alert_State ON app.Alert (State, Severity) INCLUDE (ObjectId, EscalationLevel, CreatedAtUtc, LastEscalatedAtUtc);
GO

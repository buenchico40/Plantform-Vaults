-- PlatformVault · Índice IX_Notification_Status
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Notification_Status' AND object_id = OBJECT_ID(N'app.Notification'))
    CREATE INDEX IX_Notification_Status ON app.Notification (Status, CreatedAtUtc);
GO

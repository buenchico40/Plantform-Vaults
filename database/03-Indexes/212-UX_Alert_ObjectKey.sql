-- PlatformVault · Índice UX_Alert_ObjectKey
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Alert_ObjectKey' AND object_id = OBJECT_ID(N'app.Alert'))
    CREATE UNIQUE INDEX UX_Alert_ObjectKey ON app.Alert (ObjectId, AlertKey);
GO

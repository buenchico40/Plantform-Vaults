-- PlatformVault · Índice IX_TemporaryAccess_Expiry
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TemporaryAccess_Expiry' AND object_id = OBJECT_ID(N'app.TemporaryAccess'))
    CREATE INDEX IX_TemporaryAccess_Expiry ON app.TemporaryAccess (State, EndUtc);
GO

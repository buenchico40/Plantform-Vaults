-- PlatformVault · Índice IX_PasswordHistory_User
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PasswordHistory_User' AND object_id = OBJECT_ID(N'[identity].PasswordHistory'))
    CREATE INDEX IX_PasswordHistory_User ON [identity].PasswordHistory (UserId, CreatedAtUtc DESC);
GO

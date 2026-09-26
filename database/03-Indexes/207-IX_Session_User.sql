-- PlatformVault · Índice IX_Session_User
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Session_User' AND object_id = OBJECT_ID(N'[identity].[Session]'))
    CREATE INDEX IX_Session_User ON [identity].[Session] (UserId) INCLUDE (RevokedAtUtc, ExpiresAtUtc);
GO

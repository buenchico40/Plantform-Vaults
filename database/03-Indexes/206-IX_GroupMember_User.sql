-- PlatformVault · Índice IX_GroupMember_User
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_GroupMember_User' AND object_id = OBJECT_ID(N'app.GroupMember'))
    CREATE INDEX IX_GroupMember_User ON app.GroupMember (UserId);
GO

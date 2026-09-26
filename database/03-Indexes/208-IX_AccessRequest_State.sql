-- PlatformVault · Índice IX_AccessRequest_State
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AccessRequest_State' AND object_id = OBJECT_ID(N'app.AccessRequest'))
    CREATE INDEX IX_AccessRequest_State ON app.AccessRequest (State, PendingExpiresAtUtc) INCLUDE (ObjectId, RequesterId, ApproverKind);
GO

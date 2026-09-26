-- PlatformVault · Índice IX_AccessRequest_Requester
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AccessRequest_Requester' AND object_id = OBJECT_ID(N'app.AccessRequest'))
    CREATE INDEX IX_AccessRequest_Requester ON app.AccessRequest (RequesterId, CreatedAtUtc DESC);
GO

-- PlatformVault · Índice IX_ObjectGroup_Group
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ObjectGroup_Group' AND object_id = OBJECT_ID(N'app.ObjectGroup'))
    CREATE INDEX IX_ObjectGroup_Group ON app.ObjectGroup (GroupId);
GO

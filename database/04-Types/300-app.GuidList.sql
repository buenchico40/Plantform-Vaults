-- PlatformVault · Tipo de tabla app.GuidList
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF TYPE_ID(N'app.GuidList') IS NULL
    CREATE TYPE app.GuidList AS TABLE (Id uniqueidentifier NOT NULL PRIMARY KEY);
GO

-- PlatformVault · Esquemas (IMP-32)
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF SCHEMA_ID('identity') IS NULL EXEC('CREATE SCHEMA [identity] AUTHORIZATION dbo');
IF SCHEMA_ID('app')      IS NULL EXEC('CREATE SCHEMA [app] AUTHORIZATION dbo');
IF SCHEMA_ID('vault')    IS NULL EXEC('CREATE SCHEMA [vault] AUTHORIZATION dbo');
IF SCHEMA_ID('audit')    IS NULL EXEC('CREATE SCHEMA [audit] AUTHORIZATION dbo');
IF SCHEMA_ID('report')   IS NULL EXEC('CREATE SCHEMA [report] AUTHORIZATION dbo');
GO

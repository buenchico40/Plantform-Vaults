-- PlatformVault · Restricción app.CK_ManagedObject_Environment
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.CK_ManagedObject_Environment') IS NULL
    ALTER TABLE app.ManagedObject ADD CONSTRAINT CK_ManagedObject_Environment CHECK (Environment IN ('Production','DisasterRecovery','PreProduction','QA','Development'));
GO

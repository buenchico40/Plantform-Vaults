-- PlatformVault · Restricción app.CK_ManagedObject_Sensitivity
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.CK_ManagedObject_Sensitivity') IS NULL
    ALTER TABLE app.ManagedObject ADD CONSTRAINT CK_ManagedObject_Sensitivity CHECK (Sensitivity IN ('Public','Internal','Confidential','Restricted'));
GO

-- PlatformVault · Restricción app.CK_ManagedObject_PayloadSensitivity
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.CK_ManagedObject_PayloadSensitivity') IS NULL
    ALTER TABLE app.ManagedObject ADD CONSTRAINT CK_ManagedObject_PayloadSensitivity CHECK (HasPayload = 0 OR Sensitivity IN ('Confidential','Restricted'));
GO

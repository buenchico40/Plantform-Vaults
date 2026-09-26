-- PlatformVault · Restricción app.CK_ManagedObject_Criticality
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.CK_ManagedObject_Criticality') IS NULL
    ALTER TABLE app.ManagedObject ADD CONSTRAINT CK_ManagedObject_Criticality CHECK (Criticality IN ('Critical','High','Medium','Low'));
GO

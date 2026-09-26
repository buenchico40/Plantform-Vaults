-- PlatformVault · Restricción app.CK_ManagedObject_ObjectType
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.CK_ManagedObject_ObjectType') IS NULL
    ALTER TABLE app.ManagedObject ADD CONSTRAINT CK_ManagedObject_ObjectType CHECK (ObjectType IN ('Certificate','CryptographicKey','Secret','Credential','ServiceAccount'));
GO

-- PlatformVault · Restricción app.CK_ManagedObject_CustodyMode
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.CK_ManagedObject_CustodyMode') IS NULL
    ALTER TABLE app.ManagedObject ADD CONSTRAINT CK_ManagedObject_CustodyMode CHECK (CustodyMode IN ('Internal','MetadataOnly'));
GO

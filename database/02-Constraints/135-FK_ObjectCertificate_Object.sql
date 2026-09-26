-- PlatformVault · Restricción app.FK_ObjectCertificate_Object
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO
IF OBJECT_ID(N'app.FK_ObjectCertificate_Object') IS NULL
    ALTER TABLE app.ObjectCertificate ADD CONSTRAINT FK_ObjectCertificate_Object FOREIGN KEY (ObjectId) REFERENCES app.ManagedObject (ObjectId);
GO

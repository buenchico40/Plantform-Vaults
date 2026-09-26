-- PlatformVault · Restricción app.FK_AccessRequest_Object
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.FK_AccessRequest_Object') IS NULL
    ALTER TABLE app.AccessRequest ADD CONSTRAINT FK_AccessRequest_Object FOREIGN KEY (ObjectId) REFERENCES app.ManagedObject (ObjectId);
GO

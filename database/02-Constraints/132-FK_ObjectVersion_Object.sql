-- PlatformVault · Restricción app.FK_ObjectVersion_Object
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.FK_ObjectVersion_Object') IS NULL
    ALTER TABLE app.ObjectVersion ADD CONSTRAINT FK_ObjectVersion_Object FOREIGN KEY (ObjectId) REFERENCES app.ManagedObject (ObjectId);
GO

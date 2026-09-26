-- PlatformVault · Restricción app.FK_ObjectGroup_Object
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.FK_ObjectGroup_Object') IS NULL
    ALTER TABLE app.ObjectGroup ADD CONSTRAINT FK_ObjectGroup_Object FOREIGN KEY (ObjectId) REFERENCES app.ManagedObject (ObjectId);
GO

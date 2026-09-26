-- PlatformVault · Restricción app.FK_Alert_Object
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.FK_Alert_Object') IS NULL
    ALTER TABLE app.Alert ADD CONSTRAINT FK_Alert_Object FOREIGN KEY (ObjectId) REFERENCES app.ManagedObject (ObjectId);
GO

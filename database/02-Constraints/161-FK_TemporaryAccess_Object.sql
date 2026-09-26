-- PlatformVault · Restricción app.FK_TemporaryAccess_Object
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.FK_TemporaryAccess_Object') IS NULL
    ALTER TABLE app.TemporaryAccess ADD CONSTRAINT FK_TemporaryAccess_Object FOREIGN KEY (ObjectId) REFERENCES app.ManagedObject (ObjectId);
GO

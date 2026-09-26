-- PlatformVault · Restricción app.FK_OwnershipHistory_Object
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.FK_OwnershipHistory_Object') IS NULL
    ALTER TABLE app.OwnershipHistory ADD CONSTRAINT FK_OwnershipHistory_Object FOREIGN KEY (ObjectId) REFERENCES app.ManagedObject (ObjectId);
GO

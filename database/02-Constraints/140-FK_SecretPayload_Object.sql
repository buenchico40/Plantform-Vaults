-- PlatformVault · Restricción vault.FK_SecretPayload_Object
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'vault.FK_SecretPayload_Object') IS NULL
    ALTER TABLE vault.SecretPayload ADD CONSTRAINT FK_SecretPayload_Object FOREIGN KEY (ObjectId) REFERENCES app.ManagedObject (ObjectId);
GO

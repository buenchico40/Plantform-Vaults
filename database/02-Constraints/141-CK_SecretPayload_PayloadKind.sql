-- PlatformVault · Restricción vault.CK_SecretPayload_PayloadKind
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'vault.CK_SecretPayload_PayloadKind') IS NULL
    ALTER TABLE vault.SecretPayload ADD CONSTRAINT CK_SecretPayload_PayloadKind CHECK (PayloadKind IN ('Text','Pkcs12','KeyMaterial'));
GO

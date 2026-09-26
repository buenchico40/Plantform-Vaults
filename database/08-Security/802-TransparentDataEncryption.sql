-- PlatformVault · TDE (RNF-SEG-03 b, DR-16)
-- Obligatorio en QA, UAT y Producción. No aplica a LocalDB (desarrollo).
-- Requiere un certificado de servidor en master creado y respaldado por el DBA:
--   USE master; CREATE MASTER KEY ...; CREATE CERTIFICATE PlatformVaultTdeCert WITH SUBJECT = 'PlatformVault TDE';
--   BACKUP CERTIFICATE PlatformVaultTdeCert TO FILE = '...' WITH PRIVATE KEY (...);
-- Variable sqlcmd: EnableTde (1 | 0).
SET NOCOUNT ON;
GO
IF '$(EnableTde)' = '1'
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.dm_database_encryption_keys WHERE database_id = DB_ID())
        EXEC('CREATE DATABASE ENCRYPTION KEY WITH ALGORITHM = AES_256 ENCRYPTION BY SERVER CERTIFICATE PlatformVaultTdeCert;');
    EXEC('ALTER DATABASE CURRENT SET ENCRYPTION ON;');
END
GO

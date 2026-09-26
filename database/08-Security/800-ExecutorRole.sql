-- PlatformVault · Rol de ejecución de la cuenta técnica (IMP-12, IMP-33)
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

-- La cuenta técnica solo puede ejecutar procedimientos; nunca lee ni escribe tablas directamente.
IF DATABASE_PRINCIPAL_ID('PlatformVaultExecutor') IS NULL
    CREATE ROLE PlatformVaultExecutor AUTHORIZATION dbo;
GO
GRANT EXECUTE ON SCHEMA::[identity] TO PlatformVaultExecutor;
GRANT EXECUTE ON SCHEMA::[app]      TO PlatformVaultExecutor;
GRANT EXECUTE ON SCHEMA::[vault]    TO PlatformVaultExecutor;
GRANT EXECUTE ON SCHEMA::[audit]    TO PlatformVaultExecutor;
GRANT EXECUTE ON SCHEMA::[report]   TO PlatformVaultExecutor;
GRANT EXECUTE ON TYPE::app.GuidList TO PlatformVaultExecutor;
DENY SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[identity] TO PlatformVaultExecutor;
DENY SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[app]      TO PlatformVaultExecutor;
DENY SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[vault]    TO PlatformVaultExecutor;
DENY SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[audit]    TO PlatformVaultExecutor;
GO

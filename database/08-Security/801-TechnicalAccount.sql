-- PlatformVault · Cuenta técnica única (prompt §2)
-- Variables sqlcmd: AppLoginName, AppLoginPassword (solo si AppLoginMode = SQL), AppLoginMode (SQL | WINDOWS | NONE).
-- En desarrollo (NONE) la API usa la cuenta de Windows del desarrollador y este script no crea login.
SET NOCOUNT ON;
GO
IF '$(AppLoginMode)' = 'SQL' AND SUSER_ID(N'$(AppLoginName)') IS NULL
    EXEC('USE master; CREATE LOGIN [$(AppLoginName)] WITH PASSWORD = N''$(AppLoginPassword)'', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;');
IF '$(AppLoginMode)' = 'WINDOWS' AND SUSER_ID(N'$(AppLoginName)') IS NULL
    EXEC('USE master; CREATE LOGIN [$(AppLoginName)] FROM WINDOWS;');
GO
IF '$(AppLoginMode)' IN ('SQL','WINDOWS') AND DATABASE_PRINCIPAL_ID(N'$(AppLoginName)') IS NULL
    EXEC('CREATE USER [$(AppLoginName)] FOR LOGIN [$(AppLoginName)];');
IF '$(AppLoginMode)' IN ('SQL','WINDOWS')
    EXEC('ALTER ROLE PlatformVaultExecutor ADD MEMBER [$(AppLoginName)];');
GO
-- Usuario sin login usado por las pruebas de permisos (10-Validation).
IF DATABASE_PRINCIPAL_ID('pv_permission_probe') IS NULL
    CREATE USER pv_permission_probe WITHOUT LOGIN;
ALTER ROLE PlatformVaultExecutor ADD MEMBER pv_permission_probe;
GO

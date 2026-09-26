-- PlatformVault · Creación de la base de datos
-- Se ejecuta contra master. Variable sqlcmd: DatabaseName.
SET NOCOUNT ON;
IF DB_ID(N'$(DatabaseName)') IS NULL
BEGIN
    CREATE DATABASE [$(DatabaseName)];
    PRINT 'Base de datos $(DatabaseName) creada.';
END
GO
ALTER DATABASE [$(DatabaseName)] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
GO

-- PlatformVault · Rollback completo (solo desarrollo y pruebas)
-- ADVERTENCIA: destruye la base de datos y la auditoría. No usar en Producción: la auditoría se conserva 10 años (RN-078).
-- Variable sqlcmd: DatabaseName, ConfirmDrop (debe valer YES).
SET NOCOUNT ON;
IF '$(ConfirmDrop)' <> 'YES'
BEGIN
    RAISERROR('Rollback cancelado: ConfirmDrop debe valer YES.', 16, 1);
    RETURN;
END
IF DB_ID(N'$(DatabaseName)') IS NOT NULL
BEGIN
    ALTER DATABASE [$(DatabaseName)] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [$(DatabaseName)];
    PRINT 'Base de datos $(DatabaseName) eliminada.';
END
GO

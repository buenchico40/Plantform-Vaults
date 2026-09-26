-- PlatformVault · Validación: la cuenta técnica solo ejecuta procedimientos (IMP-12)
SET NOCOUNT ON;
DECLARE @Denied int = 0, @Executed bit = 0;
EXECUTE AS USER = 'pv_permission_probe';
BEGIN TRY DECLARE @x int = (SELECT COUNT(*) FROM app.ManagedObject); END TRY BEGIN CATCH SET @Denied += 1; END CATCH
BEGIN TRY DECLARE @y int = (SELECT COUNT(*) FROM vault.SecretPayload); END TRY BEGIN CATCH SET @Denied += 1; END CATCH
BEGIN TRY DECLARE @z int = (SELECT COUNT(*) FROM [identity].[User]); END TRY BEGIN CATCH SET @Denied += 1; END CATCH
BEGIN TRY INSERT app.Area (AreaId, Code, Name) VALUES (NEWID(), 'PROBE', N'Probe'); END TRY BEGIN CATCH SET @Denied += 1; END CATCH
BEGIN TRY
    DECLARE @Areas TABLE (AreaId uniqueidentifier, Code varchar(30), Name nvarchar(150), IsActive bit);
    INSERT @Areas EXEC app.usp_Area_GetAll;
    SET @Executed = 1;
END TRY BEGIN CATCH END CATCH
REVERT;
IF @Denied <> 4 BEGIN RAISERROR(N'La cuenta técnica puede acceder directamente a tablas (%d de 4 accesos bloqueados).', 16, 1, @Denied); RETURN; END
IF @Executed = 0 BEGIN RAISERROR(N'La cuenta técnica no puede ejecutar procedimientos.', 16, 1); RETURN; END
PRINT N'OK permisos: tablas denegadas, procedimientos permitidos.';
GO

-- PlatformVault · Validación: la auditoría no admite UPDATE ni DELETE (RN-075)
SET NOCOUNT ON;
DECLARE @Blocked int = 0;
-- El bloqueo del Ledger (Msg 37359) es un error de compilación: se prueba en un lote hijo con texto constante
-- para poder capturarlo. No se construye SQL a partir de datos.
BEGIN TRY EXEC (N'UPDATE audit.AuditEvent SET Details = Details WHERE 1 = 0;'); END TRY BEGIN CATCH IF ERROR_NUMBER() = 37359 SET @Blocked += 1; END CATCH
BEGIN TRY EXEC (N'DELETE audit.AuditEvent WHERE 1 = 0;'); END TRY BEGIN CATCH IF ERROR_NUMBER() = 37359 SET @Blocked += 1; END CATCH
BEGIN TRY EXEC (N'DELETE audit.UsageEvent WHERE 1 = 0;'); END TRY BEGIN CATCH IF ERROR_NUMBER() = 37359 SET @Blocked += 1; END CATCH
IF @Blocked <> 3 BEGIN RAISERROR(N'La auditoría admite modificaciones: revisar LEDGER APPEND_ONLY.', 16, 1); RETURN; END
PRINT N'OK auditoría de solo inserción.';
GO

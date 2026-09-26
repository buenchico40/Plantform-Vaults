-- PlatformVault · Restricción app.CK_Alert_Severity
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.CK_Alert_Severity') IS NULL
    ALTER TABLE app.Alert ADD CONSTRAINT CK_Alert_Severity CHECK (Severity IN ('Low','Medium','High','Critical'));
GO

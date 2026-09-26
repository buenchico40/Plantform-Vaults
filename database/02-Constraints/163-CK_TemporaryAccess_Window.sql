-- PlatformVault · Restricción app.CK_TemporaryAccess_Window
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.CK_TemporaryAccess_Window') IS NULL
    ALTER TABLE app.TemporaryAccess ADD CONSTRAINT CK_TemporaryAccess_Window CHECK (EndUtc > StartUtc);
GO

-- PlatformVault · Restricción app.CK_TemporaryAccess_State
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.CK_TemporaryAccess_State') IS NULL
    ALTER TABLE app.TemporaryAccess ADD CONSTRAINT CK_TemporaryAccess_State CHECK (State IN ('Scheduled','Active','Expired','Revoked'));
GO

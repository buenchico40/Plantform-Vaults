-- PlatformVault · Restricción app.CK_Alert_State
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.CK_Alert_State') IS NULL
    ALTER TABLE app.Alert ADD CONSTRAINT CK_Alert_State CHECK (State IN ('Open','Acknowledged','Escalated','Resolved'));
GO

-- PlatformVault · Restricción app.CK_AccessRequest_State
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.CK_AccessRequest_State') IS NULL
    ALTER TABLE app.AccessRequest ADD CONSTRAINT CK_AccessRequest_State CHECK (State IN ('Pending','Approved','Rejected','Cancelled','Expired'));
GO

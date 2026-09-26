-- PlatformVault · Restricción app.CK_AccessRequest_Action
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.CK_AccessRequest_Action') IS NULL
    ALTER TABLE app.AccessRequest ADD CONSTRAINT CK_AccessRequest_Action CHECK (Action IN ('Reveal','DownloadPrivateKey','DownloadKeyMaterial'));
GO

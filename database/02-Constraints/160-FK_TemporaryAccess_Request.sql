-- PlatformVault · Restricción app.FK_TemporaryAccess_Request
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.FK_TemporaryAccess_Request') IS NULL
    ALTER TABLE app.TemporaryAccess ADD CONSTRAINT FK_TemporaryAccess_Request FOREIGN KEY (RequestId) REFERENCES app.AccessRequest (RequestId);
GO

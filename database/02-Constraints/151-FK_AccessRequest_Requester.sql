-- PlatformVault · Restricción app.FK_AccessRequest_Requester
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.FK_AccessRequest_Requester') IS NULL
    ALTER TABLE app.AccessRequest ADD CONSTRAINT FK_AccessRequest_Requester FOREIGN KEY (RequesterId) REFERENCES [identity].[User] (UserId);
GO

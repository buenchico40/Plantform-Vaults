-- PlatformVault · Restricción [identity].FK_Session_User
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'[identity].FK_Session_User') IS NULL
    ALTER TABLE [identity].[Session] ADD CONSTRAINT FK_Session_User FOREIGN KEY (UserId) REFERENCES [identity].[User] (UserId);
GO

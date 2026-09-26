-- PlatformVault · Restricción [identity].FK_PasswordHistory_User
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'[identity].FK_PasswordHistory_User') IS NULL
    ALTER TABLE [identity].PasswordHistory ADD CONSTRAINT FK_PasswordHistory_User FOREIGN KEY (UserId) REFERENCES [identity].[User] (UserId);
GO

-- PlatformVault · Restricción [identity].FK_UserRole_User
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'[identity].FK_UserRole_User') IS NULL
    ALTER TABLE [identity].UserRole ADD CONSTRAINT FK_UserRole_User FOREIGN KEY (UserId) REFERENCES [identity].[User] (UserId);
GO

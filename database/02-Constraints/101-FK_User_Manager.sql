-- PlatformVault · Restricción [identity].FK_User_Manager
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'[identity].FK_User_Manager') IS NULL
    ALTER TABLE [identity].[User] ADD CONSTRAINT FK_User_Manager FOREIGN KEY (ManagerUserId) REFERENCES [identity].[User] (UserId);
GO

-- PlatformVault · Restricción [identity].FK_UserRole_Role
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'[identity].FK_UserRole_Role') IS NULL
    ALTER TABLE [identity].UserRole ADD CONSTRAINT FK_UserRole_Role FOREIGN KEY (RoleId) REFERENCES [identity].[Role] (RoleId);
GO

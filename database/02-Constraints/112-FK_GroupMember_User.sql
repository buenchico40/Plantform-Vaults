-- PlatformVault · Restricción app.FK_GroupMember_User
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.FK_GroupMember_User') IS NULL
    ALTER TABLE app.GroupMember ADD CONSTRAINT FK_GroupMember_User FOREIGN KEY (UserId) REFERENCES [identity].[User] (UserId);
GO

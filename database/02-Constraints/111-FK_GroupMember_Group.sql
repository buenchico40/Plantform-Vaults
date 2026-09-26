-- PlatformVault · Restricción app.FK_GroupMember_Group
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.FK_GroupMember_Group') IS NULL
    ALTER TABLE app.GroupMember ADD CONSTRAINT FK_GroupMember_Group FOREIGN KEY (GroupId) REFERENCES app.SecurityGroup (GroupId);
GO

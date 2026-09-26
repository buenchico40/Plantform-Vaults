-- PlatformVault · Restricción app.FK_ObjectGroup_Group
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.FK_ObjectGroup_Group') IS NULL
    ALTER TABLE app.ObjectGroup ADD CONSTRAINT FK_ObjectGroup_Group FOREIGN KEY (GroupId) REFERENCES app.SecurityGroup (GroupId);
GO

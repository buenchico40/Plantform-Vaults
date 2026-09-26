-- PlatformVault · Restricción [identity].FK_User_Area
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'[identity].FK_User_Area') IS NULL
    ALTER TABLE [identity].[User] ADD CONSTRAINT FK_User_Area FOREIGN KEY (AreaId) REFERENCES app.Area (AreaId);
GO

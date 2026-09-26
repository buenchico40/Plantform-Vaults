-- PlatformVault · Restricción app.FK_ManagedObject_Owner
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.FK_ManagedObject_Owner') IS NULL
    ALTER TABLE app.ManagedObject ADD CONSTRAINT FK_ManagedObject_Owner FOREIGN KEY (OwnerId) REFERENCES [identity].[User] (UserId);
GO

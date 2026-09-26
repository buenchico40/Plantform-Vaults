-- PlatformVault · Restricción app.FK_ManagedObject_FunctionalOwner
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.FK_ManagedObject_FunctionalOwner') IS NULL
    ALTER TABLE app.ManagedObject ADD CONSTRAINT FK_ManagedObject_FunctionalOwner FOREIGN KEY (FunctionalOwnerId) REFERENCES [identity].[User] (UserId);
GO

-- PlatformVault · Restricción app.FK_ManagedObject_TechnicalOwner
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.FK_ManagedObject_TechnicalOwner') IS NULL
    ALTER TABLE app.ManagedObject ADD CONSTRAINT FK_ManagedObject_TechnicalOwner FOREIGN KEY (TechnicalOwnerId) REFERENCES [identity].[User] (UserId);
GO

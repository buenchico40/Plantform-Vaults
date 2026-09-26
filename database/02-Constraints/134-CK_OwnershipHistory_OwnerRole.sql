-- PlatformVault · Restricción app.CK_OwnershipHistory_OwnerRole
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.CK_OwnershipHistory_OwnerRole') IS NULL
    ALTER TABLE app.OwnershipHistory ADD CONSTRAINT CK_OwnershipHistory_OwnerRole CHECK (OwnerRole IN ('Functional','Technical'));
GO

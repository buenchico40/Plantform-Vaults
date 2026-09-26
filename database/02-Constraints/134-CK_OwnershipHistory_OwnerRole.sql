-- PlatformVault · Restricción app.CK_OwnershipHistory_OwnerRole
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

-- IMP-62: los cambios nuevos se registran como 'Owner'; 'Functional' y 'Technical' se conservan como historial.
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_OwnershipHistory_OwnerRole' AND definition NOT LIKE N'%Owner''%')
    ALTER TABLE app.OwnershipHistory DROP CONSTRAINT CK_OwnershipHistory_OwnerRole;
GO
IF OBJECT_ID(N'app.CK_OwnershipHistory_OwnerRole') IS NULL
    ALTER TABLE app.OwnershipHistory ADD CONSTRAINT CK_OwnershipHistory_OwnerRole CHECK (OwnerRole IN ('Functional','Technical','Owner'));
GO

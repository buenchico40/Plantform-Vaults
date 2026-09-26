-- PlatformVault · Restricción app.CK_AccessRequest_Duration
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.CK_AccessRequest_Duration') IS NULL
    ALTER TABLE app.AccessRequest ADD CONSTRAINT CK_AccessRequest_Duration CHECK (DurationMinutes BETWEEN 1 AND 1440);
GO

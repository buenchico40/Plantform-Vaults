-- PlatformVault · Restricción app.CK_AccessRequest_ApproverKind
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.CK_AccessRequest_ApproverKind') IS NULL
    ALTER TABLE app.AccessRequest ADD CONSTRAINT CK_AccessRequest_ApproverKind CHECK (ApproverKind IN ('Security','GroupPeer','Owner'));
GO

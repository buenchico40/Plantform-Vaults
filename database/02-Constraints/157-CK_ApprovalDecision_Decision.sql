-- PlatformVault · Restricción app.CK_ApprovalDecision_Decision
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.CK_ApprovalDecision_Decision') IS NULL
    ALTER TABLE app.ApprovalDecision ADD CONSTRAINT CK_ApprovalDecision_Decision CHECK (Decision IN ('Approved','Rejected'));
GO

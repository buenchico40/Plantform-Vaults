-- PlatformVault · Restricción app.FK_ApprovalDecision_Request
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.FK_ApprovalDecision_Request') IS NULL
    ALTER TABLE app.ApprovalDecision ADD CONSTRAINT FK_ApprovalDecision_Request FOREIGN KEY (RequestId) REFERENCES app.AccessRequest (RequestId);
GO

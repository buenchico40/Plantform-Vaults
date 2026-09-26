-- PlatformVault · Tabla app.ApprovalDecision
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.ApprovalDecision', N'U') IS NULL
BEGIN
    CREATE TABLE app.ApprovalDecision (
        DecisionId   bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_ApprovalDecision PRIMARY KEY,
        RequestId    uniqueidentifier NOT NULL,
        ApproverId   uniqueidentifier NOT NULL,
        Decision     varchar(10)      NOT NULL,
        Comment      nvarchar(1000)   NULL,
        DecidedAtUtc datetime2(3)     NOT NULL
    );
END
GO

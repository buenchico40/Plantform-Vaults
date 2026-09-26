-- PlatformVault · Tabla app.Alert
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.Alert', N'U') IS NULL
BEGIN
    CREATE TABLE app.Alert (
        AlertId            uniqueidentifier NOT NULL CONSTRAINT PK_Alert PRIMARY KEY,
        ObjectId           uniqueidentifier NOT NULL,
        AlertKey           varchar(20)      NOT NULL,
        Kind               varchar(15)      NOT NULL,
        ThresholdDays      int              NULL,
        Severity           varchar(10)      NOT NULL,
        State              varchar(15)      NOT NULL,
        EscalationLevel    tinyint          NOT NULL CONSTRAINT DF_Alert_EscalationLevel DEFAULT (1),
        CreatedAtUtc       datetime2(3)     NOT NULL,
        LastEscalatedAtUtc datetime2(3)     NULL,
        AcknowledgedBy     uniqueidentifier NULL,
        AcknowledgedAtUtc  datetime2(3)     NULL,
        AcknowledgeComment nvarchar(500)    NULL,
        ResolvedAtUtc      datetime2(3)     NULL,
        ResolutionReason   nvarchar(200)    NULL
    );
END
GO

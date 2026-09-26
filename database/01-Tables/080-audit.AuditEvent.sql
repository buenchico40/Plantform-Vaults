-- PlatformVault · Tabla audit.AuditEvent
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'audit.AuditEvent', N'U') IS NULL
BEGIN
    -- Ledger de solo inserción (RN-075, RNF-AUD-02). Hash SHA-256 encadenado calculado en audit.usp_AuditEvent_Append (RN-076).
    CREATE TABLE audit.AuditEvent (
        SequenceNumber bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_AuditEvent PRIMARY KEY,
        EventId        uniqueidentifier NOT NULL,
        TimestampUtc   datetime2(3)     NOT NULL,
        ActorType      varchar(15)      NOT NULL,
        ActorId        nvarchar(100)    NULL,
        ActorName      nvarchar(200)    NULL,
        Action         varchar(60)      NOT NULL,
        ResourceType   varchar(40)      NULL,
        ResourceId     nvarchar(100)    NULL,
        Result         varchar(10)      NOT NULL,
        SourceIp       varchar(45)      NULL,
        CorrelationId  uniqueidentifier NULL,
        Details        nvarchar(4000)   NULL,
        PreviousHash   binary(32)       NULL,
        Hash           binary(32)       NOT NULL
    ) WITH (LEDGER = ON (APPEND_ONLY = ON));
END
GO

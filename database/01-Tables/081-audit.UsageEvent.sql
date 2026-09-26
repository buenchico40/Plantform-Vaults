-- PlatformVault · Tabla audit.UsageEvent
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'audit.UsageEvent', N'U') IS NULL
BEGIN
    -- Eventos de uso (RN-072). Ledger de solo inserción.
    CREATE TABLE audit.UsageEvent (
        UsageEventId  bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_UsageEvent PRIMARY KEY,
        ObjectId      uniqueidentifier NOT NULL,
        ActorType     varchar(15)      NOT NULL,
        ActorId       nvarchar(100)    NOT NULL,
        Action        varchar(30)      NOT NULL,
        TimestampUtc  datetime2(3)     NOT NULL,
        Channel       varchar(10)      NOT NULL,
        Result        varchar(10)      NOT NULL,
        SourceIp      varchar(45)      NULL,
        CorrelationId uniqueidentifier NULL
    ) WITH (LEDGER = ON (APPEND_ONLY = ON));
END
GO

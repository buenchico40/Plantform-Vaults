-- PlatformVault · Tabla app.AccessRequest
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.AccessRequest', N'U') IS NULL
BEGIN
    CREATE TABLE app.AccessRequest (
        RequestId           uniqueidentifier NOT NULL CONSTRAINT PK_AccessRequest PRIMARY KEY,
        Code                varchar(12)      NOT NULL CONSTRAINT UQ_AccessRequest_Code UNIQUE,
        RequesterId         uniqueidentifier NOT NULL,
        ObjectId            uniqueidentifier NOT NULL,
        Action              varchar(20)      NOT NULL,
        Justification       nvarchar(1000)   NOT NULL,
        RequestedStartUtc   datetime2(3)     NOT NULL,
        DurationMinutes     int              NOT NULL,
        ApproverKind        varchar(15)      NOT NULL,
        State               varchar(15)      NOT NULL,
        CreatedAtUtc        datetime2(3)     NOT NULL,
        PendingExpiresAtUtc datetime2(3)     NOT NULL,
        DecidedAtUtc        datetime2(3)     NULL,
        RowVer              rowversion       NOT NULL
    );
END
GO

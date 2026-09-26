-- PlatformVault · Tabla app.TemporaryAccess
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.TemporaryAccess', N'U') IS NULL
BEGIN
    CREATE TABLE app.TemporaryAccess (
        AccessId      uniqueidentifier NOT NULL CONSTRAINT PK_TemporaryAccess PRIMARY KEY,
        RequestId     uniqueidentifier NOT NULL CONSTRAINT UQ_TemporaryAccess_RequestId UNIQUE,
        ObjectId      uniqueidentifier NOT NULL,
        BeneficiaryId uniqueidentifier NOT NULL,
        Action        varchar(20)      NOT NULL,
        StartUtc      datetime2(3)     NOT NULL,
        EndUtc        datetime2(3)     NOT NULL,
        State         varchar(15)      NOT NULL,
        RevokedAtUtc  datetime2(3)     NULL,
        RevokedBy     uniqueidentifier NULL,
        RevokeReason  nvarchar(300)    NULL
    );
END
GO

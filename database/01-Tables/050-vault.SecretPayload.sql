-- PlatformVault · Tabla vault.SecretPayload
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'vault.SecretPayload', N'U') IS NULL
BEGIN
    CREATE TABLE vault.SecretPayload (
        ObjectId       uniqueidentifier NOT NULL,
        VersionNumber  int              NOT NULL,
        Component      tinyint          NOT NULL CONSTRAINT DF_SecretPayload_Component DEFAULT (0),
        PayloadKind    varchar(20)      NOT NULL,
        Ciphertext     varbinary(max)   NOT NULL,
        Nonce          binary(12)       NOT NULL,
        Tag            binary(16)       NOT NULL,
        WrappedDek     varbinary(1024)  NOT NULL,
        KekThumbprint  varchar(64)      NOT NULL,
        Algorithm      varchar(40)      NOT NULL,
        CreatedAtUtc   datetime2(3)     NOT NULL CONSTRAINT DF_SecretPayload_CreatedAtUtc DEFAULT (sysutcdatetime()),
        CreatedBy      uniqueidentifier NOT NULL,
        CONSTRAINT PK_SecretPayload PRIMARY KEY (ObjectId, VersionNumber, Component)
    );
END
GO

-- PlatformVault · Tabla app.ExpirationPolicy
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.ExpirationPolicy', N'U') IS NULL
BEGIN
    CREATE TABLE app.ExpirationPolicy (
        PolicyId      uniqueidentifier NOT NULL CONSTRAINT PK_ExpirationPolicy PRIMARY KEY,
        Name          nvarchar(150)    NOT NULL,
        ObjectType    varchar(20)      NULL,
        Criticality   varchar(10)      NULL,
        ThresholdDays varchar(100)     NOT NULL,
        IsActive      bit              NOT NULL CONSTRAINT DF_ExpirationPolicy_IsActive DEFAULT (1),
        ModifiedAtUtc datetime2(3)     NOT NULL CONSTRAINT DF_ExpirationPolicy_ModifiedAtUtc DEFAULT (sysutcdatetime()),
        ModifiedBy    uniqueidentifier NULL
    );
END
GO

-- PlatformVault · Tabla app.ManagedObject
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.ManagedObject', N'U') IS NULL
BEGIN
    CREATE TABLE app.ManagedObject (
        ObjectId              uniqueidentifier NOT NULL CONSTRAINT PK_ManagedObject PRIMARY KEY,
        Code                  varchar(12)      NOT NULL CONSTRAINT UQ_ManagedObject_Code UNIQUE,
        ObjectType            varchar(20)      NOT NULL,
        Subtype               varchar(50)      NOT NULL,
        Name                  nvarchar(200)    NOT NULL,
        Description           nvarchar(1000)   NULL,
        Criticality           varchar(10)      NOT NULL,
        Sensitivity           varchar(15)      NOT NULL,
        Environment           varchar(20)      NOT NULL,
        AreaId                uniqueidentifier NOT NULL,
        FunctionalOwnerId     uniqueidentifier NULL,
        TechnicalOwnerId      uniqueidentifier NULL,
        LifecycleState        varchar(15)      NOT NULL,
        CustodyMode           varchar(15)      NOT NULL,
        HasPayload            bit              NOT NULL CONSTRAINT DF_ManagedObject_HasPayload DEFAULT (0),
        ExpirationDate        datetime2(0)     NULL,
        NoExpirationJustified bit              NOT NULL CONSTRAINT DF_ManagedObject_NoExpiration DEFAULT (0),
        Thumbprint            char(64)         NULL,
        DetailsJson           nvarchar(max)    NULL,
        CurrentVersion        int              NOT NULL CONSTRAINT DF_ManagedObject_CurrentVersion DEFAULT (1),
        CreatedAtUtc          datetime2(3)     NOT NULL CONSTRAINT DF_ManagedObject_CreatedAtUtc DEFAULT (sysutcdatetime()),
        CreatedBy             uniqueidentifier NOT NULL,
        ModifiedAtUtc         datetime2(3)     NULL,
        ModifiedBy            uniqueidentifier NULL,
        RowVer                rowversion       NOT NULL
    );
END
GO

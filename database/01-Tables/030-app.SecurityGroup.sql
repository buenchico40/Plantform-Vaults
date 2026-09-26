-- PlatformVault · Tabla app.SecurityGroup
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.SecurityGroup', N'U') IS NULL
BEGIN
    CREATE TABLE app.SecurityGroup (
        GroupId       uniqueidentifier NOT NULL CONSTRAINT PK_SecurityGroup PRIMARY KEY,
        Code          varchar(12)      NOT NULL CONSTRAINT UQ_SecurityGroup_Code UNIQUE,
        Name          nvarchar(150)    NOT NULL CONSTRAINT UQ_SecurityGroup_Name UNIQUE,
        Description   nvarchar(500)    NULL,
        AreaId        uniqueidentifier NULL,
        IsActive      bit              NOT NULL CONSTRAINT DF_SecurityGroup_IsActive DEFAULT (1),
        CreatedAtUtc  datetime2(3)     NOT NULL CONSTRAINT DF_SecurityGroup_CreatedAtUtc DEFAULT (sysutcdatetime()),
        CreatedBy     uniqueidentifier NOT NULL,
        ModifiedAtUtc datetime2(3)     NULL,
        ModifiedBy    uniqueidentifier NULL
    );
END
GO

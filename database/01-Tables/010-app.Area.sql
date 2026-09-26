-- PlatformVault · Tabla app.Area
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.Area', N'U') IS NULL
BEGIN
    CREATE TABLE app.Area (
        AreaId        uniqueidentifier NOT NULL CONSTRAINT PK_Area PRIMARY KEY,
        Code          varchar(30)      NOT NULL CONSTRAINT UQ_Area_Code UNIQUE,
        Name          nvarchar(150)    NOT NULL,
        IsActive      bit              NOT NULL CONSTRAINT DF_Area_IsActive DEFAULT (1),
        CreatedAtUtc  datetime2(3)     NOT NULL CONSTRAINT DF_Area_CreatedAtUtc DEFAULT (sysutcdatetime())
    );
END
GO

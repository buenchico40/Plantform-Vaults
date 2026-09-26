-- PlatformVault · Tabla [identity].[Role]
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'[identity].[Role]', N'U') IS NULL
BEGIN
    CREATE TABLE [identity].[Role] (
        RoleId           uniqueidentifier NOT NULL CONSTRAINT PK_Role PRIMARY KEY,
        Name             nvarchar(50)     NOT NULL CONSTRAINT UQ_Role_Name UNIQUE,
        NormalizedName   nvarchar(50)     NOT NULL CONSTRAINT UQ_Role_NormalizedName UNIQUE,
        IsAssignable     bit              NOT NULL,
        IsExclusive      bit              NOT NULL,
        ConcurrencyStamp nvarchar(100)    NOT NULL
    );
END
GO

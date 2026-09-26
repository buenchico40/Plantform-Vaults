-- PlatformVault · Tabla [identity].[User]
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'[identity].[User]', N'U') IS NULL
BEGIN
    CREATE TABLE [identity].[User] (
        UserId               uniqueidentifier  NOT NULL CONSTRAINT PK_User PRIMARY KEY,
        UserName             nvarchar(100)     NOT NULL,
        NormalizedUserName   nvarchar(100)     NOT NULL CONSTRAINT UQ_User_NormalizedUserName UNIQUE,
        Email                nvarchar(256)     NULL,
        NormalizedEmail      nvarchar(256)     NULL,
        DisplayName          nvarchar(200)     NOT NULL,
        PasswordHash         nvarchar(512)     NULL,
        SecurityStamp        nvarchar(100)     NOT NULL,
        ConcurrencyStamp     nvarchar(100)     NOT NULL,
        LockoutEnabled       bit               NOT NULL CONSTRAINT DF_User_LockoutEnabled DEFAULT (1),
        LockoutEndUtc        datetimeoffset(3) NULL,
        AccessFailedCount    int               NOT NULL CONSTRAINT DF_User_AccessFailedCount DEFAULT (0),
        IsActive             bit               NOT NULL CONSTRAINT DF_User_IsActive DEFAULT (1),
        AreaId               uniqueidentifier  NULL,
        ManagerUserId        uniqueidentifier  NULL,
        PasswordChangedAtUtc datetime2(3)      NULL,
        MustChangePassword   bit               NOT NULL CONSTRAINT DF_User_MustChangePassword DEFAULT (1),
        CreatedAtUtc         datetime2(3)      NOT NULL CONSTRAINT DF_User_CreatedAtUtc DEFAULT (sysutcdatetime()),
        CreatedBy            uniqueidentifier  NULL,
        ModifiedAtUtc        datetime2(3)      NULL,
        ModifiedBy           uniqueidentifier  NULL
    );
END
GO

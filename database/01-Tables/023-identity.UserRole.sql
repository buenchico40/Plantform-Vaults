-- PlatformVault · Tabla [identity].UserRole
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'[identity].UserRole', N'U') IS NULL
BEGIN
    CREATE TABLE [identity].UserRole (
        UserId        uniqueidentifier NOT NULL,
        RoleId        uniqueidentifier NOT NULL,
        AssignedAtUtc datetime2(3)     NOT NULL CONSTRAINT DF_UserRole_AssignedAtUtc DEFAULT (sysutcdatetime()),
        AssignedBy    uniqueidentifier NULL,
        CONSTRAINT PK_UserRole PRIMARY KEY (UserId, RoleId)
    );
END
GO

-- PlatformVault · Tabla [identity].PasswordHistory
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'[identity].PasswordHistory', N'U') IS NULL
BEGIN
    CREATE TABLE [identity].PasswordHistory (
        PasswordHistoryId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_PasswordHistory PRIMARY KEY,
        UserId            uniqueidentifier NOT NULL,
        PasswordHash      nvarchar(512)    NOT NULL,
        CreatedAtUtc      datetime2(3)     NOT NULL CONSTRAINT DF_PasswordHistory_CreatedAtUtc DEFAULT (sysutcdatetime())
    );
END
GO

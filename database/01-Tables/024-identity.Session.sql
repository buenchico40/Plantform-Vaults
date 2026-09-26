-- PlatformVault · Tabla [identity].[Session]
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'[identity].[Session]', N'U') IS NULL
BEGIN
    CREATE TABLE [identity].[Session] (
        SessionId        uniqueidentifier NOT NULL CONSTRAINT PK_Session PRIMARY KEY,
        UserId           uniqueidentifier NOT NULL,
        TokenHash        binary(32)       NOT NULL CONSTRAINT UQ_Session_TokenHash UNIQUE,
        CreatedAtUtc     datetime2(3)     NOT NULL,
        LastActivityUtc  datetime2(3)     NOT NULL,
        ExpiresAtUtc     datetime2(3)     NOT NULL,
        LastReauthUtc    datetime2(3)     NULL,
        RevokedAtUtc     datetime2(3)     NULL,
        RevokeReason     nvarchar(100)    NULL,
        ClientIp         varchar(45)      NULL,
        UserAgent        nvarchar(300)    NULL
    );
END
GO

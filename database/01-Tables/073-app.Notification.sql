-- PlatformVault · Tabla app.Notification
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.Notification', N'U') IS NULL
BEGIN
    CREATE TABLE app.Notification (
        NotificationId    bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_Notification PRIMARY KEY,
        Channel           varchar(15)    NOT NULL,
        Recipient         nvarchar(256)  NOT NULL,
        Subject           nvarchar(300)  NOT NULL,
        Body              nvarchar(max)  NOT NULL,
        Status            varchar(15)    NOT NULL,
        Attempts          int            NOT NULL CONSTRAINT DF_Notification_Attempts DEFAULT (0),
        CreatedAtUtc      datetime2(3)   NOT NULL CONSTRAINT DF_Notification_CreatedAtUtc DEFAULT (sysutcdatetime()),
        LastAttemptAtUtc  datetime2(3)   NULL,
        LastError         nvarchar(1000) NULL
    );
END
GO

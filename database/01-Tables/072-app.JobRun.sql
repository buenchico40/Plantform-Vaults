-- PlatformVault · Tabla app.JobRun
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.JobRun', N'U') IS NULL
BEGIN
    CREATE TABLE app.JobRun (
        RunId          bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_JobRun PRIMARY KEY,
        JobName        varchar(60)    NOT NULL,
        StartedAtUtc   datetime2(3)   NOT NULL,
        FinishedAtUtc  datetime2(3)   NULL,
        Status         varchar(15)    NOT NULL,
        ItemsProcessed int            NOT NULL CONSTRAINT DF_JobRun_ItemsProcessed DEFAULT (0),
        Detail         nvarchar(2000) NULL
    );
END
GO

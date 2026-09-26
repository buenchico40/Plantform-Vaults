-- PlatformVault · Tabla app.OwnershipHistory
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.OwnershipHistory', N'U') IS NULL
BEGIN
    CREATE TABLE app.OwnershipHistory (
        OwnershipHistoryId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_OwnershipHistory PRIMARY KEY,
        ObjectId           uniqueidentifier NOT NULL,
        OwnerRole          varchar(10)      NOT NULL,
        PreviousUserId     uniqueidentifier NULL,
        NewUserId          uniqueidentifier NOT NULL,
        ChangedBy          uniqueidentifier NOT NULL,
        ChangedAtUtc       datetime2(3)     NOT NULL,
        Reason             nvarchar(500)    NOT NULL
    );
END
GO

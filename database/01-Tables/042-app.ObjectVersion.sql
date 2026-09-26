-- PlatformVault · Tabla app.ObjectVersion
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.ObjectVersion', N'U') IS NULL
BEGIN
    CREATE TABLE app.ObjectVersion (
        ObjectId          uniqueidentifier NOT NULL,
        VersionNumber     int              NOT NULL,
        ChangedBy         uniqueidentifier NOT NULL,
        ChangedAtUtc      datetime2(3)     NOT NULL,
        Reason            nvarchar(500)    NOT NULL,
        ChangedFieldsJson nvarchar(max)    NULL,
        CONSTRAINT PK_ObjectVersion PRIMARY KEY (ObjectId, VersionNumber)
    );
END
GO

-- PlatformVault · Tabla app.ObjectGroup
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.ObjectGroup', N'U') IS NULL
BEGIN
    CREATE TABLE app.ObjectGroup (
        ObjectId      uniqueidentifier NOT NULL,
        GroupId       uniqueidentifier NOT NULL,
        AssignedAtUtc datetime2(3)     NOT NULL CONSTRAINT DF_ObjectGroup_AssignedAtUtc DEFAULT (sysutcdatetime()),
        AssignedBy    uniqueidentifier NOT NULL,
        CONSTRAINT PK_ObjectGroup PRIMARY KEY (ObjectId, GroupId)
    );
END
GO

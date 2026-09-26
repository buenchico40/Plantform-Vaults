-- PlatformVault · Tabla app.GroupMember
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.GroupMember', N'U') IS NULL
BEGIN
    CREATE TABLE app.GroupMember (
        GroupId       uniqueidentifier NOT NULL,
        UserId        uniqueidentifier NOT NULL,
        IsResponsible bit              NOT NULL CONSTRAINT DF_GroupMember_IsResponsible DEFAULT (0),
        AddedAtUtc    datetime2(3)     NOT NULL CONSTRAINT DF_GroupMember_AddedAtUtc DEFAULT (sysutcdatetime()),
        AddedBy       uniqueidentifier NOT NULL,
        CONSTRAINT PK_GroupMember PRIMARY KEY (GroupId, UserId)
    );
END
GO

-- PlatformVault · Tabla app.ManagedObject
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.ManagedObject', N'U') IS NULL
BEGIN
    CREATE TABLE app.ManagedObject (
        ObjectId              uniqueidentifier NOT NULL CONSTRAINT PK_ManagedObject PRIMARY KEY,
        Code                  varchar(12)      NOT NULL CONSTRAINT UQ_ManagedObject_Code UNIQUE,
        ObjectType            varchar(20)      NOT NULL,
        Subtype               varchar(50)      NOT NULL,
        Name                  nvarchar(200)    NOT NULL,
        Description           nvarchar(1000)   NULL,
        Criticality           varchar(10)      NOT NULL,
        Sensitivity           varchar(15)      NOT NULL,
        Environment           varchar(20)      NOT NULL,
        AreaId                uniqueidentifier NOT NULL,
        OwnerId               uniqueidentifier NULL,
        LifecycleState        varchar(15)      NOT NULL,
        CustodyMode           varchar(15)      NOT NULL,
        HasPayload            bit              NOT NULL CONSTRAINT DF_ManagedObject_HasPayload DEFAULT (0),
        ExpirationDate        datetime2(0)     NULL,
        NoExpirationJustified bit              NOT NULL CONSTRAINT DF_ManagedObject_NoExpiration DEFAULT (0),
        Thumbprint            char(64)         NULL,
        DetailsJson           nvarchar(max)    NULL,
        CurrentVersion        int              NOT NULL CONSTRAINT DF_ManagedObject_CurrentVersion DEFAULT (1),
        CreatedAtUtc          datetime2(3)     NOT NULL CONSTRAINT DF_ManagedObject_CreatedAtUtc DEFAULT (sysutcdatetime()),
        CreatedBy             uniqueidentifier NOT NULL,
        ModifiedAtUtc         datetime2(3)     NULL,
        ModifiedBy            uniqueidentifier NULL,
        RowVer                rowversion       NOT NULL
    );
END
GO

-- IMP-62: migración de bases existentes de dos propietarios (funcional y técnico) a un propietario único.
-- Se conserva el técnico y, si falta, el funcional. Se ejecuta una sola vez: mientras exista FunctionalOwnerId.
IF COL_LENGTH(N'app.ManagedObject', N'FunctionalOwnerId') IS NOT NULL AND COL_LENGTH(N'app.ManagedObject', N'OwnerId') IS NULL
    ALTER TABLE app.ManagedObject ADD OwnerId uniqueidentifier NULL;
GO
IF COL_LENGTH(N'app.ManagedObject', N'FunctionalOwnerId') IS NOT NULL
BEGIN
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    EXEC (N'UPDATE app.ManagedObject SET OwnerId = COALESCE(TechnicalOwnerId, FunctionalOwnerId);');
    -- Alertas abiertas: de cuatro a tres niveles (N1 y N2 → N1, N3 → N2, N4 → N3).
    IF OBJECT_ID(N'app.Alert', N'U') IS NOT NULL
        EXEC (N'UPDATE app.Alert SET EscalationLevel = CASE WHEN EscalationLevel <= 2 THEN 1 ELSE EscalationLevel - 1 END
                WHERE State IN (''Open'', ''Escalated'');');
    IF OBJECT_ID(N'app.FK_ManagedObject_FunctionalOwner') IS NOT NULL
        ALTER TABLE app.ManagedObject DROP CONSTRAINT FK_ManagedObject_FunctionalOwner;
    IF OBJECT_ID(N'app.FK_ManagedObject_TechnicalOwner') IS NOT NULL
        ALTER TABLE app.ManagedObject DROP CONSTRAINT FK_ManagedObject_TechnicalOwner;
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ManagedObject_Owners' AND object_id = OBJECT_ID(N'app.ManagedObject'))
        DROP INDEX IX_ManagedObject_Owners ON app.ManagedObject;
    EXEC (N'ALTER TABLE app.ManagedObject DROP COLUMN FunctionalOwnerId, TechnicalOwnerId;');
    COMMIT TRANSACTION;
END
GO

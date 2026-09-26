-- PlatformVault · Edición de metadatos con nueva versión y control de concurrencia (US-006, US-011, RN-092)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_ManagedObject_UpdateMetadata
    @ObjectId uniqueidentifier, @Subtype varchar(50), @Name nvarchar(200), @Description nvarchar(1000) = NULL,
    @Criticality varchar(10), @Sensitivity varchar(15), @Environment varchar(20),
    @ExpirationDate datetime2(0) = NULL, @NoExpirationJustified bit, @DetailsJson nvarchar(max) = NULL,
    @ExpectedRowVer binary(8), @ModifiedBy uniqueidentifier, @NowUtc datetime2(3), @Reason nvarchar(500),
    @ChangedFieldsJson nvarchar(max) = NULL, @ResolveOpenAlerts bit = 0
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    UPDATE app.ManagedObject
       SET Subtype = @Subtype, Name = @Name, Description = @Description, Criticality = @Criticality, Sensitivity = @Sensitivity,
           Environment = @Environment, ExpirationDate = @ExpirationDate, NoExpirationJustified = @NoExpirationJustified,
           DetailsJson = @DetailsJson, CurrentVersion = CurrentVersion + 1, ModifiedAtUtc = @NowUtc, ModifiedBy = @ModifiedBy
     WHERE ObjectId = @ObjectId AND RowVer = @ExpectedRowVer;
    IF @@ROWCOUNT = 0 THROW 51001, N'CONCURRENCY_CONFLICT', 1;

    DECLARE @Version int = (SELECT CurrentVersion FROM app.ManagedObject WHERE ObjectId = @ObjectId);
    INSERT app.ObjectVersion (ObjectId, VersionNumber, ChangedBy, ChangedAtUtc, Reason, ChangedFieldsJson)
    VALUES (@ObjectId, @Version, @ModifiedBy, @NowUtc, @Reason, @ChangedFieldsJson);

    -- RN-066: al cambiar la fecha de expiración, las alertas abiertas se resuelven y el monitoreo las recalcula.
    IF @ResolveOpenAlerts = 1
        UPDATE app.Alert SET State = 'Resolved', ResolvedAtUtc = @NowUtc, ResolutionReason = N'ExpirationChanged'
        WHERE ObjectId = @ObjectId AND State <> 'Resolved';
    COMMIT TRANSACTION;

    SELECT o.RowVer, o.CurrentVersion FROM app.ManagedObject AS o WHERE o.ObjectId = @ObjectId;
END
GO

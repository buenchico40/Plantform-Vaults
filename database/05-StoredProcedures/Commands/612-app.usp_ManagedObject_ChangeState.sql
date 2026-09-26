-- PlatformVault · Cambio de estado del ciclo de vida (US-007, RN-007, RN-009, RN-066)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_ManagedObject_ChangeState
    @ObjectId uniqueidentifier, @NewState varchar(15), @ExpectedRowVer binary(8), @ModifiedBy uniqueidentifier,
    @NowUtc datetime2(3), @Reason nvarchar(500), @ChangedFieldsJson nvarchar(max) = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    UPDATE app.ManagedObject
       SET LifecycleState = @NewState, CurrentVersion = CurrentVersion + 1, ModifiedAtUtc = @NowUtc, ModifiedBy = @ModifiedBy
     WHERE ObjectId = @ObjectId AND RowVer = @ExpectedRowVer;
    IF @@ROWCOUNT = 0 THROW 51001, N'CONCURRENCY_CONFLICT', 1;

    DECLARE @Version int = (SELECT CurrentVersion FROM app.ManagedObject WHERE ObjectId = @ObjectId);
    INSERT app.ObjectVersion (ObjectId, VersionNumber, ChangedBy, ChangedAtUtc, Reason, ChangedFieldsJson)
    VALUES (@ObjectId, @Version, @ModifiedBy, @NowUtc, @Reason, @ChangedFieldsJson);

    DECLARE @RevokedAccesses int = 0, @CancelledRequests int = 0;
    IF @NewState IN ('Suspended', 'Deactivated')
    BEGIN
        -- RN-009: se revocan los accesos temporales vigentes y se cancelan las solicitudes pendientes.
        UPDATE app.TemporaryAccess SET State = 'Revoked', RevokedAtUtc = @NowUtc, RevokedBy = @ModifiedBy,
               RevokeReason = N'ObjectState:' + @NewState
         WHERE ObjectId = @ObjectId AND State IN ('Scheduled', 'Active');
        SET @RevokedAccesses = @@ROWCOUNT;
        UPDATE app.AccessRequest SET State = 'Cancelled', DecidedAtUtc = @NowUtc
         WHERE ObjectId = @ObjectId AND State = 'Pending';
        SET @CancelledRequests = @@ROWCOUNT;
    END
    IF @NewState = 'Deactivated'
        UPDATE app.Alert SET State = 'Resolved', ResolvedAtUtc = @NowUtc, ResolutionReason = N'ObjectDeactivated'
        WHERE ObjectId = @ObjectId AND State <> 'Resolved';
    COMMIT TRANSACTION;

    SELECT o.RowVer, o.CurrentVersion, @RevokedAccesses AS RevokedAccesses, @CancelledRequests AS CancelledRequests
    FROM app.ManagedObject AS o WHERE o.ObjectId = @ObjectId;
END
GO

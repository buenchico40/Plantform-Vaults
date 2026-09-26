-- PlatformVault · Asignación del propietario con historial (US-013, RN-087, RN-089, IMP-62)
SET NOCOUNT ON;
GO
-- IMP-62: el objeto tiene un único propietario; sustituye a usp_ManagedObject_SetOwners (funcional y técnico).
DROP PROCEDURE IF EXISTS app.usp_ManagedObject_SetOwners;
GO
CREATE OR ALTER PROCEDURE app.usp_ManagedObject_SetOwner
    @ObjectId uniqueidentifier, @OwnerId uniqueidentifier,
    @ExpectedRowVer binary(8), @ModifiedBy uniqueidentifier, @NowUtc datetime2(3), @Reason nvarchar(500),
    @ChangedFieldsJson nvarchar(max) = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    DECLARE @PrevOwner uniqueidentifier;
    SELECT @PrevOwner = OwnerId FROM app.ManagedObject WITH (UPDLOCK) WHERE ObjectId = @ObjectId;

    UPDATE app.ManagedObject
       SET OwnerId = @OwnerId, CurrentVersion = CurrentVersion + 1, ModifiedAtUtc = @NowUtc, ModifiedBy = @ModifiedBy
     WHERE ObjectId = @ObjectId AND RowVer = @ExpectedRowVer;
    IF @@ROWCOUNT = 0 THROW 51001, N'CONCURRENCY_CONFLICT', 1;

    IF ISNULL(@PrevOwner, '00000000-0000-0000-0000-000000000000') <> @OwnerId
        INSERT app.OwnershipHistory (ObjectId, OwnerRole, PreviousUserId, NewUserId, ChangedBy, ChangedAtUtc, Reason)
        VALUES (@ObjectId, 'Owner', @PrevOwner, @OwnerId, @ModifiedBy, @NowUtc, @Reason);

    DECLARE @Version int = (SELECT CurrentVersion FROM app.ManagedObject WHERE ObjectId = @ObjectId);
    INSERT app.ObjectVersion (ObjectId, VersionNumber, ChangedBy, ChangedAtUtc, Reason, ChangedFieldsJson)
    VALUES (@ObjectId, @Version, @ModifiedBy, @NowUtc, @Reason, @ChangedFieldsJson);
    COMMIT TRANSACTION;

    SELECT o.RowVer, o.CurrentVersion FROM app.ManagedObject AS o WHERE o.ObjectId = @ObjectId;
END
GO

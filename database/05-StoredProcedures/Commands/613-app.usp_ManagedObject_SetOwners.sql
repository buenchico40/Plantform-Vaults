-- PlatformVault · Asignación de propietarios con historial (US-013, RN-087 a RN-089)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_ManagedObject_SetOwners
    @ObjectId uniqueidentifier, @FunctionalOwnerId uniqueidentifier, @TechnicalOwnerId uniqueidentifier,
    @ExpectedRowVer binary(8), @ModifiedBy uniqueidentifier, @NowUtc datetime2(3), @Reason nvarchar(500),
    @ChangedFieldsJson nvarchar(max) = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    DECLARE @PrevFunctional uniqueidentifier, @PrevTechnical uniqueidentifier;
    SELECT @PrevFunctional = FunctionalOwnerId, @PrevTechnical = TechnicalOwnerId
    FROM app.ManagedObject WITH (UPDLOCK) WHERE ObjectId = @ObjectId;

    UPDATE app.ManagedObject
       SET FunctionalOwnerId = @FunctionalOwnerId, TechnicalOwnerId = @TechnicalOwnerId, CurrentVersion = CurrentVersion + 1,
           ModifiedAtUtc = @NowUtc, ModifiedBy = @ModifiedBy
     WHERE ObjectId = @ObjectId AND RowVer = @ExpectedRowVer;
    IF @@ROWCOUNT = 0 THROW 51001, N'CONCURRENCY_CONFLICT', 1;

    IF ISNULL(@PrevFunctional, '00000000-0000-0000-0000-000000000000') <> @FunctionalOwnerId
        INSERT app.OwnershipHistory (ObjectId, OwnerRole, PreviousUserId, NewUserId, ChangedBy, ChangedAtUtc, Reason)
        VALUES (@ObjectId, 'Functional', @PrevFunctional, @FunctionalOwnerId, @ModifiedBy, @NowUtc, @Reason);
    IF ISNULL(@PrevTechnical, '00000000-0000-0000-0000-000000000000') <> @TechnicalOwnerId
        INSERT app.OwnershipHistory (ObjectId, OwnerRole, PreviousUserId, NewUserId, ChangedBy, ChangedAtUtc, Reason)
        VALUES (@ObjectId, 'Technical', @PrevTechnical, @TechnicalOwnerId, @ModifiedBy, @NowUtc, @Reason);

    DECLARE @Version int = (SELECT CurrentVersion FROM app.ManagedObject WHERE ObjectId = @ObjectId);
    INSERT app.ObjectVersion (ObjectId, VersionNumber, ChangedBy, ChangedAtUtc, Reason, ChangedFieldsJson)
    VALUES (@ObjectId, @Version, @ModifiedBy, @NowUtc, @Reason, @ChangedFieldsJson);
    COMMIT TRANSACTION;

    SELECT o.RowVer, o.CurrentVersion FROM app.ManagedObject AS o WHERE o.ObjectId = @ObjectId;
END
GO

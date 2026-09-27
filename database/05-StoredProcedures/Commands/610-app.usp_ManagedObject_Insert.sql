-- PlatformVault · Registro de objeto administrado en estado Borrador con versión 1 y sus grupos (US-001 a US-005, US-028, RN-001, RN-092)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_ManagedObject_Insert
    @ObjectId uniqueidentifier, @ObjectType varchar(20), @Subtype varchar(50), @Name nvarchar(200), @Description nvarchar(1000) = NULL,
    @Criticality varchar(10), @Sensitivity varchar(15), @Environment varchar(20), @AreaId uniqueidentifier,
    @OwnerId uniqueidentifier = NULL, @CustodyMode varchar(15), @HasPayload bit,
    @ExpirationDate datetime2(0) = NULL, @NoExpirationJustified bit, @Thumbprint char(64) = NULL, @DetailsJson nvarchar(max) = NULL,
    @ModifiedBy uniqueidentifier, @NowUtc datetime2(3), @Reason nvarchar(500), @ChangedFieldsJson nvarchar(max) = NULL,
    @GroupIds app.GuidList READONLY
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    DECLARE @Code varchar(12) = 'OBJ-' + RIGHT('000000' + CAST(NEXT VALUE FOR app.ObjectCodeSeq AS varchar(10)), 6);

    INSERT app.ManagedObject (ObjectId, Code, ObjectType, Subtype, Name, Description, Criticality, Sensitivity, Environment, AreaId,
        OwnerId, LifecycleState, CustodyMode, HasPayload, ExpirationDate, NoExpirationJustified, Thumbprint,
        DetailsJson, CurrentVersion, CreatedAtUtc, CreatedBy)
    VALUES (@ObjectId, @Code, @ObjectType, @Subtype, @Name, @Description, @Criticality, @Sensitivity, @Environment, @AreaId,
        @OwnerId, 'Draft', @CustodyMode, @HasPayload, @ExpirationDate, @NoExpirationJustified, @Thumbprint,
        @DetailsJson, 1, @NowUtc, @ModifiedBy);

    INSERT app.ObjectVersion (ObjectId, VersionNumber, ChangedBy, ChangedAtUtc, Reason, ChangedFieldsJson)
    VALUES (@ObjectId, 1, @ModifiedBy, @NowUtc, @Reason, @ChangedFieldsJson);

    IF @OwnerId IS NOT NULL
        INSERT app.OwnershipHistory (ObjectId, OwnerRole, PreviousUserId, NewUserId, ChangedBy, ChangedAtUtc, Reason)
        VALUES (@ObjectId, 'Owner', NULL, @OwnerId, @ModifiedBy, @NowUtc, @Reason);

    -- IMP-63: los grupos elegidos en el alta se asignan en la misma transacción (versión 1).
    INSERT app.ObjectGroup (ObjectId, GroupId, AssignedAtUtc, AssignedBy)
    SELECT DISTINCT @ObjectId, g.Id, @NowUtc, @ModifiedBy FROM @GroupIds AS g;

    SELECT o.Code, o.RowVer, o.CurrentVersion FROM app.ManagedObject AS o WHERE o.ObjectId = @ObjectId;
END
GO

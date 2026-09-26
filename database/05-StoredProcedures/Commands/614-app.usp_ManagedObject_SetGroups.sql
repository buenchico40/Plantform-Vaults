-- PlatformVault · Asignación del objeto a grupos de acceso (US-028)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_ManagedObject_SetGroups
    @ObjectId uniqueidentifier, @GroupIds app.GuidList READONLY, @ExpectedRowVer binary(8), @ModifiedBy uniqueidentifier,
    @NowUtc datetime2(3), @Reason nvarchar(500), @ChangedFieldsJson nvarchar(max) = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    UPDATE app.ManagedObject SET CurrentVersion = CurrentVersion + 1, ModifiedAtUtc = @NowUtc, ModifiedBy = @ModifiedBy
     WHERE ObjectId = @ObjectId AND RowVer = @ExpectedRowVer;
    IF @@ROWCOUNT = 0 THROW 51001, N'CONCURRENCY_CONFLICT', 1;

    DELETE og FROM app.ObjectGroup AS og WHERE og.ObjectId = @ObjectId AND NOT EXISTS (SELECT 1 FROM @GroupIds AS g WHERE g.Id = og.GroupId);
    INSERT app.ObjectGroup (ObjectId, GroupId, AssignedAtUtc, AssignedBy)
    SELECT @ObjectId, g.Id, @NowUtc, @ModifiedBy FROM @GroupIds AS g
    WHERE NOT EXISTS (SELECT 1 FROM app.ObjectGroup AS og WHERE og.ObjectId = @ObjectId AND og.GroupId = g.Id);

    DECLARE @Version int = (SELECT CurrentVersion FROM app.ManagedObject WHERE ObjectId = @ObjectId);
    INSERT app.ObjectVersion (ObjectId, VersionNumber, ChangedBy, ChangedAtUtc, Reason, ChangedFieldsJson)
    VALUES (@ObjectId, @Version, @ModifiedBy, @NowUtc, @Reason, @ChangedFieldsJson);
    COMMIT TRANSACTION;

    SELECT o.RowVer, o.CurrentVersion FROM app.ManagedObject AS o WHERE o.ObjectId = @ObjectId;
END
GO

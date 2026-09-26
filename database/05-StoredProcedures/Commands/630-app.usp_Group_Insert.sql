-- PlatformVault · Alta de grupo de acceso con su responsable (US-027, RN-033, RN-034)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_Group_Insert
    @GroupId uniqueidentifier, @Name nvarchar(150), @Description nvarchar(500) = NULL, @AreaId uniqueidentifier = NULL,
    @ResponsibleUserId uniqueidentifier, @CreatedBy uniqueidentifier, @NowUtc datetime2(3)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    DECLARE @Code varchar(12) = 'GRP-' + RIGHT('000000' + CAST(NEXT VALUE FOR app.GroupCodeSeq AS varchar(10)), 6);
    INSERT app.SecurityGroup (GroupId, Code, Name, Description, AreaId, IsActive, CreatedAtUtc, CreatedBy)
    VALUES (@GroupId, @Code, @Name, @Description, @AreaId, 1, @NowUtc, @CreatedBy);
    INSERT app.GroupMember (GroupId, UserId, IsResponsible, AddedAtUtc, AddedBy)
    VALUES (@GroupId, @ResponsibleUserId, 1, @NowUtc, @CreatedBy);
    COMMIT TRANSACTION;
    SELECT @Code AS Code;
END
GO

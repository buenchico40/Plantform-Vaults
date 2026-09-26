-- PlatformVault · Alta o cambio de responsabilidad de un miembro (US-027, RN-034)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_GroupMember_Upsert
    @GroupId uniqueidentifier, @UserId uniqueidentifier, @IsResponsible bit, @AddedBy uniqueidentifier, @NowUtc datetime2(3)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF EXISTS (SELECT 1 FROM app.GroupMember WHERE GroupId = @GroupId AND UserId = @UserId)
        UPDATE app.GroupMember SET IsResponsible = @IsResponsible WHERE GroupId = @GroupId AND UserId = @UserId;
    ELSE
        INSERT app.GroupMember (GroupId, UserId, IsResponsible, AddedAtUtc, AddedBy) VALUES (@GroupId, @UserId, @IsResponsible, @NowUtc, @AddedBy);
END
GO

-- PlatformVault · Detalle de grupo con miembros (US-027)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_Group_GetById @GroupId uniqueidentifier, @ViewerUserId uniqueidentifier, @HasGlobalScope bit
AS
BEGIN
    SET NOCOUNT ON;
    IF @HasGlobalScope = 0 AND NOT EXISTS (SELECT 1 FROM app.GroupMember WHERE GroupId = @GroupId AND UserId = @ViewerUserId)
        RETURN;
    SELECT g.GroupId, g.Code, g.Name, g.Description, g.AreaId, g.IsActive, g.CreatedAtUtc, g.CreatedBy, g.ModifiedAtUtc,
           (SELECT COUNT(*) FROM app.ObjectGroup AS og WHERE og.GroupId = g.GroupId) AS ObjectCount
    FROM app.SecurityGroup AS g WHERE g.GroupId = @GroupId;

    SELECT gm.UserId, u.UserName, u.DisplayName, u.Email, u.IsActive, gm.IsResponsible, gm.AddedAtUtc
    FROM app.GroupMember AS gm JOIN [identity].[User] AS u ON u.UserId = gm.UserId
    WHERE gm.GroupId = @GroupId
    ORDER BY gm.IsResponsible DESC, u.DisplayName;
END
GO

-- PlatformVault · Pertenencias y propiedades que impiden roles exclusivos (RN-103)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE [identity].usp_User_GetAssignmentConstraints @UserId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;
    SELECT (SELECT COUNT(*) FROM app.GroupMember WHERE UserId = @UserId) AS GroupMemberships,
           (SELECT COUNT(*) FROM app.ManagedObject WHERE (FunctionalOwnerId = @UserId OR TechnicalOwnerId = @UserId)
               AND LifecycleState <> 'Deactivated') AS OwnedObjects;
END
GO

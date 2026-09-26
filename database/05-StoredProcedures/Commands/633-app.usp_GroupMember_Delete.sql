-- PlatformVault · Retiro de un miembro con revocación de accesos derivados (RN-035)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_GroupMember_Delete
    @GroupId uniqueidentifier, @UserId uniqueidentifier, @RemovedBy uniqueidentifier, @NowUtc datetime2(3)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    IF EXISTS (SELECT 1 FROM app.GroupMember WHERE GroupId = @GroupId AND UserId = @UserId AND IsResponsible = 1)
       AND (SELECT COUNT(*) FROM app.GroupMember WHERE GroupId = @GroupId AND IsResponsible = 1) = 1
        THROW 51003, N'INVALID_STATE|last_responsible', 1;

    DELETE app.GroupMember WHERE GroupId = @GroupId AND UserId = @UserId;
    IF @@ROWCOUNT = 0 THROW 51002, N'NOT_FOUND|member', 1;

    -- Accesos y solicitudes sobre objetos del grupo, salvo que el usuario conserve visibilidad por propiedad u otro grupo activo.
    DECLARE @Affected TABLE (ObjectId uniqueidentifier PRIMARY KEY);
    INSERT @Affected (ObjectId)
    SELECT og.ObjectId FROM app.ObjectGroup AS og
    JOIN app.ManagedObject AS o ON o.ObjectId = og.ObjectId
    WHERE og.GroupId = @GroupId
      AND ISNULL(o.FunctionalOwnerId, '00000000-0000-0000-0000-000000000000') <> @UserId
      AND ISNULL(o.TechnicalOwnerId, '00000000-0000-0000-0000-000000000000') <> @UserId
      AND NOT EXISTS (SELECT 1 FROM app.ObjectGroup AS og2
                      JOIN app.SecurityGroup AS g2 ON g2.GroupId = og2.GroupId AND g2.IsActive = 1
                      JOIN app.GroupMember AS gm2 ON gm2.GroupId = og2.GroupId AND gm2.UserId = @UserId
                      WHERE og2.ObjectId = og.ObjectId AND og2.GroupId <> @GroupId);

    UPDATE ta SET State = 'Revoked', RevokedAtUtc = @NowUtc, RevokedBy = @RemovedBy, RevokeReason = N'RemovedFromGroup'
    FROM app.TemporaryAccess AS ta JOIN @Affected AS a ON a.ObjectId = ta.ObjectId
    WHERE ta.BeneficiaryId = @UserId AND ta.State IN ('Scheduled', 'Active');
    DECLARE @Revoked int = @@ROWCOUNT;

    UPDATE ar SET State = 'Cancelled', DecidedAtUtc = @NowUtc
    FROM app.AccessRequest AS ar JOIN @Affected AS a ON a.ObjectId = ar.ObjectId
    WHERE ar.RequesterId = @UserId AND ar.State = 'Pending';
    DECLARE @Cancelled int = @@ROWCOUNT;
    COMMIT TRANSACTION;
    SELECT @Revoked AS RevokedAccesses, @Cancelled AS CancelledRequests;
END
GO

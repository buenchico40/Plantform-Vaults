-- PlatformVault · Actualización de grupo (US-027, RN-035)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_Group_Update
    @GroupId uniqueidentifier, @Name nvarchar(150), @Description nvarchar(500) = NULL, @IsActive bit,
    @ModifiedBy uniqueidentifier, @NowUtc datetime2(3)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    DECLARE @WasActive bit = (SELECT IsActive FROM app.SecurityGroup WITH (UPDLOCK) WHERE GroupId = @GroupId);
    IF @WasActive IS NULL THROW 51002, N'NOT_FOUND|group', 1;
    UPDATE app.SecurityGroup SET Name = @Name, Description = @Description, IsActive = @IsActive, ModifiedAtUtc = @NowUtc, ModifiedBy = @ModifiedBy
    WHERE GroupId = @GroupId;

    DECLARE @Revoked int = 0;
    IF @WasActive = 1 AND @IsActive = 0
    BEGIN
        -- RN-035: al desactivar el grupo se revocan los accesos obtenidos por esa pertenencia
        -- (salvo para los propietarios del objeto, que conservan su acceso por propiedad).
        UPDATE ta SET State = 'Revoked', RevokedAtUtc = @NowUtc, RevokedBy = @ModifiedBy, RevokeReason = N'GroupDeactivated'
        FROM app.TemporaryAccess AS ta
        JOIN app.ManagedObject AS o ON o.ObjectId = ta.ObjectId
        WHERE ta.State IN ('Scheduled', 'Active')
          AND EXISTS (SELECT 1 FROM app.ObjectGroup AS og WHERE og.ObjectId = ta.ObjectId AND og.GroupId = @GroupId)
          AND EXISTS (SELECT 1 FROM app.GroupMember AS gm WHERE gm.GroupId = @GroupId AND gm.UserId = ta.BeneficiaryId)
          AND ISNULL(o.FunctionalOwnerId, '00000000-0000-0000-0000-000000000000') <> ta.BeneficiaryId
          AND ISNULL(o.TechnicalOwnerId, '00000000-0000-0000-0000-000000000000') <> ta.BeneficiaryId;
        SET @Revoked = @@ROWCOUNT;
    END
    COMMIT TRANSACTION;
    SELECT @Revoked AS RevokedAccesses;
END
GO

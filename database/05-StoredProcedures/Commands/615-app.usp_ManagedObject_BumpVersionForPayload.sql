-- PlatformVault · Nueva versión por cambio del valor sensible (US-006, RN-093)
SET NOCOUNT ON;
GO
-- Se invoca dentro de la transacción de la aplicación, junto con vault.usp_SecretPayload_Insert.
CREATE OR ALTER PROCEDURE app.usp_ManagedObject_BumpVersionForPayload
    @ObjectId uniqueidentifier, @ExpectedRowVer binary(8), @ModifiedBy uniqueidentifier, @NowUtc datetime2(3),
    @Reason nvarchar(500), @ChangedFieldsJson nvarchar(max) = NULL, @UpdateExpiration bit = 0, @ExpirationDate datetime2(0) = NULL,
    @UpdateCertificate bit = 0, @Thumbprint char(64) = NULL, @DetailsJson nvarchar(max) = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    -- Renovación de certificado (RN-016): la huella, los atributos y la expiración salen del archivo nuevo.
    UPDATE app.ManagedObject
       SET HasPayload = 1, CurrentVersion = CurrentVersion + 1, ModifiedAtUtc = @NowUtc, ModifiedBy = @ModifiedBy,
           ExpirationDate = CASE WHEN @UpdateExpiration = 1 THEN @ExpirationDate ELSE ExpirationDate END,
           NoExpirationJustified = CASE WHEN @UpdateExpiration = 1 THEN 0 ELSE NoExpirationJustified END,
           Thumbprint = CASE WHEN @UpdateCertificate = 1 THEN @Thumbprint ELSE Thumbprint END,
           DetailsJson = CASE WHEN @UpdateCertificate = 1 THEN @DetailsJson ELSE DetailsJson END
     WHERE ObjectId = @ObjectId AND RowVer = @ExpectedRowVer;
    IF @@ROWCOUNT = 0 THROW 51001, N'CONCURRENCY_CONFLICT', 1;

    DECLARE @Version int = (SELECT CurrentVersion FROM app.ManagedObject WHERE ObjectId = @ObjectId);
    INSERT app.ObjectVersion (ObjectId, VersionNumber, ChangedBy, ChangedAtUtc, Reason, ChangedFieldsJson)
    VALUES (@ObjectId, @Version, @ModifiedBy, @NowUtc, @Reason, @ChangedFieldsJson);
    IF @UpdateExpiration = 1
        UPDATE app.Alert SET State = 'Resolved', ResolvedAtUtc = @NowUtc, ResolutionReason = N'Renewed'
        WHERE ObjectId = @ObjectId AND State <> 'Resolved';

    SELECT o.RowVer, o.CurrentVersion FROM app.ManagedObject AS o WHERE o.ObjectId = @ObjectId;
END
GO

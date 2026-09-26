-- PlatformVault · Alta o modificación de política de expiración (US-020, RN-061, RN-062)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_ExpirationPolicy_Upsert
    @PolicyId uniqueidentifier, @Name nvarchar(150), @ObjectType varchar(20) = NULL, @Criticality varchar(10) = NULL,
    @ThresholdDays varchar(100), @IsActive bit, @ModifiedBy uniqueidentifier, @NowUtc datetime2(3)
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM app.ExpirationPolicy WHERE PolicyId = @PolicyId)
        UPDATE app.ExpirationPolicy SET Name = @Name, ObjectType = @ObjectType, Criticality = @Criticality, ThresholdDays = @ThresholdDays,
               IsActive = @IsActive, ModifiedAtUtc = @NowUtc, ModifiedBy = @ModifiedBy
        WHERE PolicyId = @PolicyId;
    ELSE
        INSERT app.ExpirationPolicy (PolicyId, Name, ObjectType, Criticality, ThresholdDays, IsActive, ModifiedAtUtc, ModifiedBy)
        VALUES (@PolicyId, @Name, @ObjectType, @Criticality, @ThresholdDays, @IsActive, @NowUtc, @ModifiedBy);
END
GO

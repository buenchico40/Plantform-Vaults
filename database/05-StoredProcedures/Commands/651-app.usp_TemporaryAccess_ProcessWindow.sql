-- PlatformVault · Activación y revocación automática por ventana de tiempo (US-035, RN-057)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_TemporaryAccess_ProcessWindow @NowUtc datetime2(3)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    UPDATE app.TemporaryAccess SET State = 'Active' WHERE State = 'Scheduled' AND StartUtc <= @NowUtc AND EndUtc > @NowUtc;
    DECLARE @Activated int = @@ROWCOUNT;
    DECLARE @Expired TABLE (AccessId uniqueidentifier, ObjectId uniqueidentifier, BeneficiaryId uniqueidentifier);
    UPDATE app.TemporaryAccess SET State = 'Expired'
    OUTPUT inserted.AccessId, inserted.ObjectId, inserted.BeneficiaryId INTO @Expired
    WHERE State IN ('Scheduled', 'Active') AND EndUtc <= @NowUtc;
    COMMIT TRANSACTION;
    SELECT @Activated AS Activated;
    SELECT AccessId, ObjectId, BeneficiaryId FROM @Expired;
END
GO

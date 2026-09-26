-- PlatformVault · Alta idempotente de alerta: una por objeto y umbral (RN-064)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_Alert_InsertIfNotExists
    @AlertId uniqueidentifier, @ObjectId uniqueidentifier, @AlertKey varchar(20), @Kind varchar(15), @ThresholdDays int = NULL,
    @Severity varchar(10), @NowUtc datetime2(3), @InitialLevel tinyint = 1
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Inserted bit = 0;
    IF NOT EXISTS (SELECT 1 FROM app.Alert WHERE ObjectId = @ObjectId AND AlertKey = @AlertKey)
    BEGIN TRY
        INSERT app.Alert (AlertId, ObjectId, AlertKey, Kind, ThresholdDays, Severity, State, EscalationLevel, CreatedAtUtc)
        VALUES (@AlertId, @ObjectId, @AlertKey, @Kind, @ThresholdDays, @Severity, 'Open', @InitialLevel, @NowUtc);
        SET @Inserted = 1;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() NOT IN (2601, 2627) THROW;
    END CATCH
    SELECT @Inserted AS Inserted;
END
GO

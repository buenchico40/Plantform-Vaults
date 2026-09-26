-- PlatformVault · Objetos a evaluar por el monitoreo diario (US-021, RN-063)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_Expiration_GetMonitoringCandidates @NowUtc datetime2(3), @HorizonDays int = 180
AS
BEGIN
    SET NOCOUNT ON;
    SELECT o.ObjectId, o.Code, o.Name, o.ObjectType, o.Criticality, o.Sensitivity, o.Environment, o.ExpirationDate,
           o.OwnerId, o.AreaId
    FROM app.ManagedObject AS o
    WHERE o.ExpirationDate IS NOT NULL AND o.LifecycleState IN ('Active', 'Suspended')
      AND o.ExpirationDate <= DATEADD(DAY, @HorizonDays, @NowUtc);
END
GO

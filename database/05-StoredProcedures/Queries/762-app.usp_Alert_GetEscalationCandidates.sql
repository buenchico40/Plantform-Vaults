-- PlatformVault · Alertas sin atender que superan su ventana (RN-069)
SET NOCOUNT ON;
GO
-- Las alertas reconocidas no escalan (RN-070). Nivel máximo 4.
CREATE OR ALTER PROCEDURE app.usp_Alert_GetEscalationCandidates
    @NowUtc datetime2(3), @CriticalHours int = 24, @HighHours int = 24, @MediumHours int = 72, @LowHours int = 168
AS
BEGIN
    SET NOCOUNT ON;
    SELECT al.AlertId, al.ObjectId, al.Severity, al.EscalationLevel, al.AlertKey
    FROM app.Alert AS al
    JOIN app.ManagedObject AS o ON o.ObjectId = al.ObjectId AND o.LifecycleState <> 'Deactivated'
    WHERE al.State IN ('Open', 'Escalated') AND al.EscalationLevel < 4
      AND DATEADD(HOUR,
            CASE al.Severity WHEN 'Critical' THEN @CriticalHours WHEN 'High' THEN @HighHours WHEN 'Medium' THEN @MediumHours ELSE @LowHours END,
            ISNULL(al.LastEscalatedAtUtc, al.CreatedAtUtc)) <= @NowUtc;
END
GO

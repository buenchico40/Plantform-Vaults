-- PlatformVault · Detalle de alerta (sin filtro: la autorización se hace sobre el objeto)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_Alert_GetById @AlertId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;
    SELECT AlertId, ObjectId, AlertKey, Kind, ThresholdDays, Severity, State, EscalationLevel, CreatedAtUtc, LastEscalatedAtUtc,
           AcknowledgedBy, AcknowledgedAtUtc, AcknowledgeComment, ResolvedAtUtc, ResolutionReason
    FROM app.Alert WHERE AlertId = @AlertId;
END
GO

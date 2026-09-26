-- PlatformVault · Escalamiento de alerta al nivel siguiente (US-023, RN-069, RN-070)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_Alert_Escalate @AlertId uniqueidentifier, @NewLevel tinyint, @NowUtc datetime2(3)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE app.Alert SET State = 'Escalated', EscalationLevel = @NewLevel, LastEscalatedAtUtc = @NowUtc
    WHERE AlertId = @AlertId AND State IN ('Open', 'Escalated') AND EscalationLevel < @NewLevel;
    SELECT @@ROWCOUNT AS Escalated;
END
GO

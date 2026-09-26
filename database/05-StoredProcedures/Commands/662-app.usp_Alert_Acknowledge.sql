-- PlatformVault · Reconocimiento de alerta: no la resuelve (US-024, RN-070)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_Alert_Acknowledge @AlertId uniqueidentifier, @UserId uniqueidentifier, @Comment nvarchar(500), @NowUtc datetime2(3)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE app.Alert SET State = 'Acknowledged', AcknowledgedBy = @UserId, AcknowledgedAtUtc = @NowUtc, AcknowledgeComment = @Comment
    WHERE AlertId = @AlertId AND State IN ('Open', 'Escalated');
    IF @@ROWCOUNT = 0 THROW 51003, N'INVALID_STATE|alert_not_open', 1;
END
GO

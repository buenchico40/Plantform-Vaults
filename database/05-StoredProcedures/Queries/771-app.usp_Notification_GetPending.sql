-- PlatformVault · Notificaciones pendientes de envío con reintentos (RN-099)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_Notification_GetPending @Top int = 50, @MaxAttempts int = 5
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@Top) NotificationId, Channel, Recipient, Subject, Body, Attempts
    FROM app.Notification
    WHERE Status IN ('Pending', 'Retry') AND Attempts < @MaxAttempts
    ORDER BY CreatedAtUtc, NotificationId;
END
GO

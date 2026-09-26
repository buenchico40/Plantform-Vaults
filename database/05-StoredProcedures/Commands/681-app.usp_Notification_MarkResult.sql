-- PlatformVault · Resultado de envío con reintentos (RN-099)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_Notification_MarkResult
    @NotificationId bigint, @Status varchar(15), @Error nvarchar(1000) = NULL, @NowUtc datetime2(3)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE app.Notification SET Status = @Status, Attempts = Attempts + 1, LastAttemptAtUtc = @NowUtc, LastError = @Error
    WHERE NotificationId = @NotificationId;
END
GO

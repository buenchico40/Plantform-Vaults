-- PlatformVault · Encolado de notificación (IMP-16). Nunca contiene valores sensibles (RN-067)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_Notification_Enqueue
    @Channel varchar(15), @Recipient nvarchar(256), @Subject nvarchar(300), @Body nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT app.Notification (Channel, Recipient, Subject, Body, Status) VALUES (@Channel, @Recipient, @Subject, @Body, 'Pending');
END
GO

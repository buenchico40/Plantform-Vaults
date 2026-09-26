-- PlatformVault · Cancelación de una solicitud propia (US-031)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_AccessRequest_Cancel @RequestId uniqueidentifier, @RequesterId uniqueidentifier, @NowUtc datetime2(3)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE app.AccessRequest SET State = 'Cancelled', DecidedAtUtc = @NowUtc
    WHERE RequestId = @RequestId AND RequesterId = @RequesterId AND State = 'Pending';
    IF @@ROWCOUNT = 0 THROW 51003, N'INVALID_STATE|request_not_pending', 1;
END
GO

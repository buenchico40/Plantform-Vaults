-- PlatformVault · Expiración de solicitudes sin respuesta (RN-049)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_AccessRequest_ExpirePending @NowUtc datetime2(3)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE app.AccessRequest SET State = 'Expired', DecidedAtUtc = @NowUtc
    OUTPUT inserted.RequestId, inserted.Code, inserted.RequesterId, inserted.ObjectId
    WHERE State = 'Pending' AND PendingExpiresAtUtc <= @NowUtc;
END
GO

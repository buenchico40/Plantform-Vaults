-- PlatformVault · Eventos de uso de un objeto
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE audit.usp_UsageEvent_ListByObject @ObjectId uniqueidentifier, @Top int = 200
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@Top) ue.UsageEventId, ue.ObjectId, ue.ActorType, ue.ActorId, u.DisplayName AS ActorName, ue.Action, ue.TimestampUtc,
           ue.Channel, ue.Result, ue.SourceIp, ue.CorrelationId
    FROM audit.UsageEvent AS ue
    LEFT JOIN [identity].[User] AS u ON CONVERT(nvarchar(100), u.UserId) = ue.ActorId
    WHERE ue.ObjectId = @ObjectId
    ORDER BY ue.TimestampUtc DESC;
END
GO

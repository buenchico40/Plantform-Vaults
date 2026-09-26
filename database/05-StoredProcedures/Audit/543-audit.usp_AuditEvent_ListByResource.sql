-- PlatformVault · Línea de tiempo de auditoría de un recurso
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE audit.usp_AuditEvent_ListByResource @ResourceType varchar(40), @ResourceId nvarchar(100), @Top int = 200
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@Top) SequenceNumber, EventId, TimestampUtc, ActorType, ActorId, ActorName, Action, ResourceType, ResourceId,
           Result, SourceIp, CorrelationId, Details, CONVERT(varchar(64), Hash, 2) AS HashHex, CAST(0 AS int) AS TotalCount
    FROM audit.AuditEvent
    WHERE ResourceType = @ResourceType AND ResourceId = @ResourceId
    ORDER BY SequenceNumber DESC;
END
GO

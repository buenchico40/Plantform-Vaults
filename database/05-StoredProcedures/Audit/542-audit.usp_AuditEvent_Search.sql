-- PlatformVault · Consulta paginada de la bitácora (US-038). Nunca contiene Sensitive Payloads (RN-085)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE audit.usp_AuditEvent_Search
    @FromUtc datetime2(3) = NULL, @ToUtc datetime2(3) = NULL, @ActorId nvarchar(100) = NULL, @Action varchar(60) = NULL,
    @ResourceType varchar(40) = NULL, @ResourceId nvarchar(100) = NULL, @Result varchar(10) = NULL,
    @CorrelationId uniqueidentifier = NULL, @PageNumber int = 1, @PageSize int = 50
AS
BEGIN
    SET NOCOUNT ON;
    IF @PageNumber < 1 SET @PageNumber = 1;
    IF @PageSize NOT BETWEEN 1 AND 500 SET @PageSize = 50;
    SELECT SequenceNumber, EventId, TimestampUtc, ActorType, ActorId, ActorName, Action, ResourceType, ResourceId, Result,
           SourceIp, CorrelationId, Details, CONVERT(varchar(64), Hash, 2) AS HashHex, COUNT(*) OVER () AS TotalCount
    FROM audit.AuditEvent
    WHERE (@FromUtc IS NULL OR TimestampUtc >= @FromUtc) AND (@ToUtc IS NULL OR TimestampUtc < @ToUtc)
      AND (@ActorId IS NULL OR ActorId = @ActorId) AND (@Action IS NULL OR Action = @Action)
      AND (@ResourceType IS NULL OR ResourceType = @ResourceType) AND (@ResourceId IS NULL OR ResourceId = @ResourceId)
      AND (@Result IS NULL OR Result = @Result) AND (@CorrelationId IS NULL OR CorrelationId = @CorrelationId)
    ORDER BY SequenceNumber DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY
    OPTION (RECOMPILE);
END
GO

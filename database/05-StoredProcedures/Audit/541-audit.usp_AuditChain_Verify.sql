-- PlatformVault · Verificación de integridad de la cadena de auditoría (RN-076)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE audit.usp_AuditChain_Verify @FromSequence bigint = NULL, @ToSequence bigint = NULL
AS
BEGIN
    SET NOCOUNT ON;
    WITH chain AS (
        SELECT e.*, LAG(e.Hash) OVER (ORDER BY e.SequenceNumber) AS PrevStored
        FROM audit.AuditEvent AS e
    ), checked AS (
        SELECT c.SequenceNumber,
               CASE WHEN ISNULL(c.PreviousHash, 0x) <> ISNULL(c.PrevStored, 0x) THEN 1
                    WHEN c.Hash <> HASHBYTES('SHA2_256', CONCAT(
            CONVERT(varchar(64), c.PrevStored, 2), N'|', CONVERT(nvarchar(36), c.EventId), N'|', CONVERT(nvarchar(33), c.TimestampUtc, 126), N'|',
            c.ActorType, N'|', c.ActorId, N'|', c.ActorName, N'|', c.Action, N'|', c.ResourceType, N'|', c.ResourceId, N'|',
            c.Result, N'|', c.SourceIp, N'|', CONVERT(nvarchar(36), c.CorrelationId), N'|', c.Details)) THEN 1
                    ELSE 0 END AS Broken
        FROM chain AS c
        WHERE (@FromSequence IS NULL OR c.SequenceNumber >= @FromSequence)
          AND (@ToSequence IS NULL OR c.SequenceNumber <= @ToSequence)
    )
    SELECT COUNT_BIG(*) AS EventsVerified,
           MIN(CASE WHEN Broken = 1 THEN SequenceNumber END) AS FirstBrokenSequence,
           MIN(SequenceNumber) AS FromSequence, MAX(SequenceNumber) AS ToSequence
    FROM checked;
END
GO

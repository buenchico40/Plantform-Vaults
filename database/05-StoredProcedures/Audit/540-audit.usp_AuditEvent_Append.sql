-- PlatformVault · Inserción en la bitácora con hash encadenado (RN-075, RN-076, RN-079)
SET NOCOUNT ON;
GO
-- Participa en la transacción del llamador: si falla, la operación de negocio se revierte (fail-closed, RN-079).
CREATE OR ALTER PROCEDURE audit.usp_AuditEvent_Append
    @EventId uniqueidentifier, @TimestampUtc datetime2(3), @ActorType varchar(15), @ActorId nvarchar(100) = NULL,
    @ActorName nvarchar(200) = NULL, @Action varchar(60), @ResourceType varchar(40) = NULL, @ResourceId nvarchar(100) = NULL,
    @Result varchar(10), @SourceIp varchar(45) = NULL, @CorrelationId uniqueidentifier = NULL, @Details nvarchar(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    DECLARE @OwnTran bit = 0;
    IF @@TRANCOUNT = 0 BEGIN BEGIN TRANSACTION; SET @OwnTran = 1; END;

    DECLARE @LockResult int;
    EXEC @LockResult = sp_getapplock @Resource = N'audit.AuditEvent.chain', @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 15000;
    IF @LockResult < 0 THROW 51005, N'AUDIT_LOCK_TIMEOUT', 1;

    DECLARE @Prev binary(32) = (SELECT TOP (1) Hash FROM audit.AuditEvent ORDER BY SequenceNumber DESC);
    DECLARE @Hash binary(32) = HASHBYTES('SHA2_256', CONCAT(
            CONVERT(varchar(64), @Prev, 2), N'|', CONVERT(nvarchar(36), @EventId), N'|', CONVERT(nvarchar(33), @TimestampUtc, 126), N'|',
            @ActorType, N'|', @ActorId, N'|', @ActorName, N'|', @Action, N'|', @ResourceType, N'|', @ResourceId, N'|',
            @Result, N'|', @SourceIp, N'|', CONVERT(nvarchar(36), @CorrelationId), N'|', @Details));

    INSERT audit.AuditEvent (EventId, TimestampUtc, ActorType, ActorId, ActorName, Action, ResourceType, ResourceId, Result,
                             SourceIp, CorrelationId, Details, PreviousHash, Hash)
    VALUES (@EventId, @TimestampUtc, @ActorType, @ActorId, @ActorName, @Action, @ResourceType, @ResourceId, @Result,
            @SourceIp, @CorrelationId, @Details, @Prev, @Hash);

    IF @OwnTran = 1 COMMIT TRANSACTION;
END
GO

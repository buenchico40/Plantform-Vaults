-- PlatformVault · Registro de uso de un objeto (RN-072)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE audit.usp_UsageEvent_Append
    @ObjectId uniqueidentifier, @ActorType varchar(15), @ActorId nvarchar(100), @Action varchar(30), @TimestampUtc datetime2(3),
    @Channel varchar(10), @Result varchar(10), @SourceIp varchar(45) = NULL, @CorrelationId uniqueidentifier = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT audit.UsageEvent (ObjectId, ActorType, ActorId, Action, TimestampUtc, Channel, Result, SourceIp, CorrelationId)
    VALUES (@ObjectId, @ActorType, @ActorId, @Action, @TimestampUtc, @Channel, @Result, @SourceIp, @CorrelationId);
END
GO

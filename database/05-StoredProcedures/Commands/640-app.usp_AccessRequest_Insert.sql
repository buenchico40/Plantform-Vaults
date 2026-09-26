-- PlatformVault · Registro de solicitud de acceso (US-031, RN-047)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_AccessRequest_Insert
    @RequestId uniqueidentifier, @RequesterId uniqueidentifier, @ObjectId uniqueidentifier, @Action varchar(20),
    @Justification nvarchar(1000), @RequestedStartUtc datetime2(3), @DurationMinutes int, @ApproverKind varchar(15),
    @NowUtc datetime2(3), @PendingExpiresAtUtc datetime2(3)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    DECLARE @Code varchar(12) = 'SOL-' + RIGHT('000000' + CAST(NEXT VALUE FOR app.RequestCodeSeq AS varchar(10)), 6);
    INSERT app.AccessRequest (RequestId, Code, RequesterId, ObjectId, Action, Justification, RequestedStartUtc, DurationMinutes,
                              ApproverKind, State, CreatedAtUtc, PendingExpiresAtUtc)
    VALUES (@RequestId, @Code, @RequesterId, @ObjectId, @Action, @Justification, @RequestedStartUtc, @DurationMinutes,
            @ApproverKind, 'Pending', @NowUtc, @PendingExpiresAtUtc);
    SELECT @Code AS Code;
END
GO

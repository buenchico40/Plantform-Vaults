-- PlatformVault · Aprobación o rechazo; al aprobar se otorga el acceso temporal (US-033, RN-051, RN-053)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_AccessRequest_Decide
    @RequestId uniqueidentifier, @ApproverId uniqueidentifier, @Decision varchar(10), @Comment nvarchar(1000) = NULL,
    @NowUtc datetime2(3), @AccessId uniqueidentifier = NULL, @StartUtc datetime2(3) = NULL, @EndUtc datetime2(3) = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    DECLARE @State varchar(15), @ObjectId uniqueidentifier, @RequesterId uniqueidentifier, @Action varchar(20);
    SELECT @State = State, @ObjectId = ObjectId, @RequesterId = RequesterId, @Action = Action
    FROM app.AccessRequest WITH (UPDLOCK, ROWLOCK) WHERE RequestId = @RequestId;
    IF @State IS NULL THROW 51002, N'NOT_FOUND|request', 1;
    IF @State <> 'Pending' THROW 51003, N'INVALID_STATE|request_not_pending', 1;

    INSERT app.ApprovalDecision (RequestId, ApproverId, Decision, Comment, DecidedAtUtc)
    VALUES (@RequestId, @ApproverId, @Decision, @Comment, @NowUtc);

    UPDATE app.AccessRequest SET State = CASE WHEN @Decision = 'Approved' THEN 'Approved' ELSE 'Rejected' END, DecidedAtUtc = @NowUtc
    WHERE RequestId = @RequestId;

    IF @Decision = 'Approved'
        INSERT app.TemporaryAccess (AccessId, RequestId, ObjectId, BeneficiaryId, Action, StartUtc, EndUtc, State)
        VALUES (@AccessId, @RequestId, @ObjectId, @RequesterId, @Action, @StartUtc, @EndUtc,
                CASE WHEN @StartUtc <= @NowUtc THEN 'Active' ELSE 'Scheduled' END);
    COMMIT TRANSACTION;
END
GO

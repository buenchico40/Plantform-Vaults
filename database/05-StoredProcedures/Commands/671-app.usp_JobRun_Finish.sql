-- PlatformVault · Fin de ejecución de trabajo
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_JobRun_Finish
    @RunId bigint, @Status varchar(15), @ItemsProcessed int, @Detail nvarchar(2000) = NULL, @NowUtc datetime2(3)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE app.JobRun SET Status = @Status, ItemsProcessed = @ItemsProcessed, Detail = @Detail, FinishedAtUtc = @NowUtc WHERE RunId = @RunId;
END
GO

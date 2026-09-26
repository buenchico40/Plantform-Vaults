-- PlatformVault · Historial de ejecuciones de trabajos (prompt §4.5)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_JobRun_ListRecent @JobName varchar(60) = NULL, @Top int = 100
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@Top) RunId, JobName, StartedAtUtc, FinishedAtUtc, Status, ItemsProcessed, Detail
    FROM app.JobRun WHERE (@JobName IS NULL OR JobName = @JobName)
    ORDER BY StartedAtUtc DESC, RunId DESC;
END
GO

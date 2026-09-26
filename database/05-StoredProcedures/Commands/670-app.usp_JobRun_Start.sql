-- PlatformVault · Inicio de ejecución de trabajo (historial, prompt §4.5)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_JobRun_Start @JobName varchar(60), @NowUtc datetime2(3)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT app.JobRun (JobName, StartedAtUtc, Status) VALUES (@JobName, @NowUtc, 'Running');
    SELECT CAST(SCOPE_IDENTITY() AS bigint) AS RunId;
END
GO

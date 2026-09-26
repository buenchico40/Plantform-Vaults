-- PlatformVault · Bloqueo de trabajo en segundo plano entre instancias (IMP-13)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_Job_AcquireLock @JobName varchar(60)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Result int;
    DECLARE @Resource nvarchar(255) = N'job:' + @JobName;
    EXEC @Result = sp_getapplock @Resource = @Resource, @LockMode = N'Exclusive', @LockOwner = N'Session', @LockTimeout = 0;
    SELECT CAST(CASE WHEN @Result >= 0 THEN 1 ELSE 0 END AS bit) AS Acquired;
END
GO

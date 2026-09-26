-- PlatformVault · Liberación del bloqueo de trabajo
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_Job_ReleaseLock @JobName varchar(60)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Resource nvarchar(255) = N'job:' + @JobName;
    IF APPLOCK_MODE('public', @Resource, 'Session') <> 'NoLock'
        EXEC sp_releaseapplock @Resource = @Resource, @LockOwner = N'Session';
END
GO

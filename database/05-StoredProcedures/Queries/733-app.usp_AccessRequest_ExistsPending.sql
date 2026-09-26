-- PlatformVault · Evita solicitudes duplicadas pendientes (RN-048)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_AccessRequest_ExistsPending
    @RequesterId uniqueidentifier, @ObjectId uniqueidentifier, @Action varchar(20)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM app.AccessRequest WHERE RequesterId = @RequesterId AND ObjectId = @ObjectId
                AND Action = @Action AND State = 'Pending') THEN 1 ELSE 0 END AS bit) AS PendingExists;
END
GO

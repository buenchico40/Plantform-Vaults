-- PlatformVault · Políticas de expiración (US-020)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_ExpirationPolicy_GetAll
AS
BEGIN
    SET NOCOUNT ON;
    SELECT PolicyId, Name, ObjectType, Criticality, ThresholdDays, IsActive, ModifiedAtUtc, ModifiedBy
    FROM app.ExpirationPolicy ORDER BY CASE WHEN ObjectType IS NULL AND Criticality IS NULL THEN 1 ELSE 0 END, Name;
END
GO

-- PlatformVault · Política de expiración global (RN-061)
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM app.ExpirationPolicy WHERE PolicyId = '7A1E0003-0000-4000-8000-000000000001')
    INSERT app.ExpirationPolicy (PolicyId, Name, ObjectType, Criticality, ThresholdDays)
    VALUES ('7A1E0003-0000-4000-8000-000000000001', N'Política global', NULL, NULL, '180,120,90,60,30,15,7,1');
GO

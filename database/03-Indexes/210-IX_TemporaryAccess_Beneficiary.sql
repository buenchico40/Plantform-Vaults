-- PlatformVault · Índice IX_TemporaryAccess_Beneficiary
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TemporaryAccess_Beneficiary' AND object_id = OBJECT_ID(N'app.TemporaryAccess'))
    CREATE INDEX IX_TemporaryAccess_Beneficiary ON app.TemporaryAccess (BeneficiaryId, ObjectId, State) INCLUDE (StartUtc, EndUtc, Action);
GO

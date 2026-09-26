-- PlatformVault · Área inicial
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM app.Area WHERE Code = 'TI')
    INSERT app.Area (AreaId, Code, Name) VALUES ('7A1E0002-0000-4000-8000-000000000001', 'TI', N'Tecnología');
GO

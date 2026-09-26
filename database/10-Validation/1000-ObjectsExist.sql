-- PlatformVault · Validación: existen las tablas, procedimientos y el Ledger esperados
SET NOCOUNT ON;
DECLARE @Missing nvarchar(max) = N'';
SELECT @Missing = @Missing + e.Name + N' '
FROM (VALUES (N'app.Area'), (N'identity.User'), (N'identity.Session'), (N'identity.Role'), (N'app.ManagedObject'),
             (N'app.SecurityGroup'), (N'app.GroupMember'), (N'vault.SecretPayload'), (N'app.AccessRequest'),
             (N'app.TemporaryAccess'), (N'app.Alert'), (N'audit.AuditEvent'), (N'audit.UsageEvent')) AS e (Name)
WHERE OBJECT_ID(QUOTENAME(PARSENAME(e.Name, 2)) + N'.' + QUOTENAME(PARSENAME(e.Name, 1)), N'U') IS NULL;
IF @Missing <> N'' BEGIN RAISERROR(N'Faltan tablas: %s', 16, 1, @Missing); RETURN; END

IF EXISTS (SELECT 1 FROM sys.tables AS t WHERE SCHEMA_NAME(t.schema_id) = 'audit' AND t.ledger_type <> 3)
BEGIN RAISERROR(N'Las tablas de auditoría deben ser Ledger de solo inserción.', 16, 1); RETURN; END

DECLARE @Procs int = (SELECT COUNT(*) FROM sys.procedures WHERE SCHEMA_NAME(schema_id) IN ('identity','app','vault','audit','report'));
DECLARE @Tables int = (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0);
PRINT CONCAT(N'OK objetos: ', @Tables, N' tablas, ', @Procs, N' procedimientos.');
GO

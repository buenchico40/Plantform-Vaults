-- PlatformVault · Restricción app.CK_ManagedObject_LifecycleState
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'app.CK_ManagedObject_LifecycleState') IS NULL
    ALTER TABLE app.ManagedObject ADD CONSTRAINT CK_ManagedObject_LifecycleState CHECK (LifecycleState IN ('Draft','Active','Suspended','Deactivated'));
GO

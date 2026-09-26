-- PlatformVault · Roles de sistema (RN-039, RN-036)
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
SET NOCOUNT ON;
GO

MERGE [identity].[Role] AS t
USING (VALUES
    ('7A1E0001-0000-4000-8000-000000000001', N'Administrador', N'ADMINISTRADOR', 1, 0),
    ('7A1E0001-0000-4000-8000-000000000002', N'Custodio',      N'CUSTODIO',      1, 0),
    ('7A1E0001-0000-4000-8000-000000000003', N'Propietario',   N'PROPIETARIO',   0, 0),
    ('7A1E0001-0000-4000-8000-000000000005', N'Auditor',       N'AUDITOR',       1, 1),
    ('7A1E0001-0000-4000-8000-000000000006', N'Seguridad',     N'SEGURIDAD',     1, 1)
) AS s (RoleId, Name, NormalizedName, IsAssignable, IsExclusive)
ON t.RoleId = s.RoleId
WHEN MATCHED THEN UPDATE SET Name = s.Name, NormalizedName = s.NormalizedName, IsAssignable = s.IsAssignable, IsExclusive = s.IsExclusive
WHEN NOT MATCHED THEN INSERT (RoleId, Name, NormalizedName, IsAssignable, IsExclusive, ConcurrencyStamp)
     VALUES (s.RoleId, s.Name, s.NormalizedName, s.IsAssignable, s.IsExclusive, CONVERT(nvarchar(100), NEWID()));
GO

-- IMP-61: el rol Operador se unifica con Custodio. Sus usuarios pasan a Custodio y el rol se elimina.
DECLARE @Operator uniqueidentifier = '7A1E0001-0000-4000-8000-000000000004';
DECLARE @Custodian uniqueidentifier = '7A1E0001-0000-4000-8000-000000000002';
IF EXISTS (SELECT 1 FROM [identity].[Role] WHERE RoleId = @Operator)
BEGIN
    BEGIN TRANSACTION;
    INSERT [identity].UserRole (UserId, RoleId, AssignedAtUtc, AssignedBy)
    SELECT ur.UserId, @Custodian, sysutcdatetime(), NULL
    FROM [identity].UserRole AS ur
    WHERE ur.RoleId = @Operator
      AND NOT EXISTS (SELECT 1 FROM [identity].UserRole AS c WHERE c.UserId = ur.UserId AND c.RoleId = @Custodian);
    DELETE FROM [identity].UserRole WHERE RoleId = @Operator;
    DELETE FROM [identity].[Role] WHERE RoleId = @Operator;
    COMMIT TRANSACTION;
END
GO

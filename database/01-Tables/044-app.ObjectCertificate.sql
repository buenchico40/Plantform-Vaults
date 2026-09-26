-- PlatformVault · Tabla app.ObjectCertificate
-- Idempotente. Ejecutar con deploy.ps1 (sqlcmd).
-- Certificado PÚBLICO (DER) de cada versión de un objeto de tipo Certificate. No es un valor sensible:
-- se descarga con el permiso Consultar (contrato: downloadObjectFile?part=PublicCertificate, IMP-42).
SET NOCOUNT ON;
GO
IF OBJECT_ID(N'app.ObjectCertificate', N'U') IS NULL
BEGIN
    CREATE TABLE app.ObjectCertificate (
        ObjectId       uniqueidentifier NOT NULL,
        VersionNumber  int              NOT NULL,
        CertificateDer varbinary(max)   NOT NULL,
        CreatedAtUtc   datetime2(3)     NOT NULL CONSTRAINT DF_ObjectCertificate_CreatedAtUtc DEFAULT (sysutcdatetime()),
        CONSTRAINT PK_ObjectCertificate PRIMARY KEY (ObjectId, VersionNumber)
    );
END
GO

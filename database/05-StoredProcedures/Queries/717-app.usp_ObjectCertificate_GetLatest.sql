-- PlatformVault · Certificado público vigente. La API lo invoca tras autorizar la consulta del objeto (IMP-42)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_ObjectCertificate_GetLatest @ObjectId uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) VersionNumber, CertificateDer
    FROM app.ObjectCertificate
    WHERE ObjectId = @ObjectId
    ORDER BY VersionNumber DESC;
END
GO

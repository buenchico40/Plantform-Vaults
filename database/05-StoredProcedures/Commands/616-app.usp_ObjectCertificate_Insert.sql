-- PlatformVault · Certificado público de una versión del objeto (RN-016, IMP-42)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_ObjectCertificate_Insert
    @ObjectId uniqueidentifier, @VersionNumber int, @CertificateDer varbinary(max)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT app.ObjectCertificate (ObjectId, VersionNumber, CertificateDer) VALUES (@ObjectId, @VersionNumber, @CertificateDer);
END
GO

-- PlatformVault · Almacenamiento del valor cifrado (IMP-18, IMP-30). Nunca recibe texto plano
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE vault.usp_SecretPayload_Insert
    @ObjectId uniqueidentifier, @VersionNumber int, @Component tinyint = 0, @PayloadKind varchar(20), @Ciphertext varbinary(max),
    @Nonce binary(12), @Tag binary(16), @WrappedDek varbinary(1024), @KekThumbprint varchar(64), @Algorithm varchar(40),
    @CreatedBy uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;
    INSERT vault.SecretPayload (ObjectId, VersionNumber, Component, PayloadKind, Ciphertext, Nonce, Tag, WrappedDek, KekThumbprint, Algorithm, CreatedBy)
    VALUES (@ObjectId, @VersionNumber, @Component, @PayloadKind, @Ciphertext, @Nonce, @Tag, @WrappedDek, @KekThumbprint, @Algorithm, @CreatedBy);
END
GO

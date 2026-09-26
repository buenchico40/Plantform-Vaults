-- PlatformVault · Lectura del valor cifrado vigente. Solo la invoca la API tras autorizar (US-036, US-037)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE vault.usp_SecretPayload_GetLatest @ObjectId uniqueidentifier, @Component tinyint = 0
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1) ObjectId, VersionNumber, Component, PayloadKind, Ciphertext, Nonce, Tag, WrappedDek, KekThumbprint, Algorithm
    FROM vault.SecretPayload
    WHERE ObjectId = @ObjectId AND Component = @Component
    ORDER BY VersionNumber DESC;
END
GO

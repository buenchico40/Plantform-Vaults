-- PlatformVault · Revocación de todas las sesiones de un usuario (bloqueo, baja, cambio de contraseña)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE [identity].usp_Session_RevokeAllForUser @UserId uniqueidentifier, @Reason nvarchar(100), @NowUtc datetime2(3), @ExceptSessionId uniqueidentifier = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE [identity].[Session] SET RevokedAtUtc = @NowUtc, RevokeReason = @Reason
    WHERE UserId = @UserId AND RevokedAtUtc IS NULL AND (@ExceptSessionId IS NULL OR SessionId <> @ExceptSessionId);
    SELECT @@ROWCOUNT AS RevokedCount;
END
GO

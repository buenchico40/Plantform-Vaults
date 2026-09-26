-- PlatformVault · Emisión de sesión opaca (IMP-26). Solo se guarda el hash del token
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE [identity].usp_Session_Insert
    @SessionId uniqueidentifier, @UserId uniqueidentifier, @TokenHash binary(32), @NowUtc datetime2(3),
    @ExpiresAtUtc datetime2(3), @ClientIp varchar(45) = NULL, @UserAgent nvarchar(300) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT [identity].[Session] (SessionId, UserId, TokenHash, CreatedAtUtc, LastActivityUtc, ExpiresAtUtc, LastReauthUtc, ClientIp, UserAgent)
    VALUES (@SessionId, @UserId, @TokenHash, @NowUtc, @NowUtc, @ExpiresAtUtc, @NowUtc, @ClientIp, @UserAgent);
END
GO

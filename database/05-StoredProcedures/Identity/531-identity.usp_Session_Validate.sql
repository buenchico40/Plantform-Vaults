-- PlatformVault · Validación de sesión: inactividad, vencimiento absoluto, revocación y estado del usuario (IMP-26)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE [identity].usp_Session_Validate
    @TokenHash binary(32), @NowUtc datetime2(3), @IdleTimeoutMinutes int
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    DECLARE @SessionId uniqueidentifier, @UserId uniqueidentifier, @LastActivity datetime2(3), @ExpiresAt datetime2(3),
            @RevokedAt datetime2(3), @UserActive bit, @LockoutEnd datetimeoffset(3);

    SELECT @SessionId = s.SessionId, @UserId = s.UserId, @LastActivity = s.LastActivityUtc, @ExpiresAt = s.ExpiresAtUtc,
           @RevokedAt = s.RevokedAtUtc, @UserActive = u.IsActive, @LockoutEnd = u.LockoutEndUtc
    FROM [identity].[Session] AS s WITH (UPDLOCK, ROWLOCK)
    JOIN [identity].[User] AS u ON u.UserId = s.UserId
    WHERE s.TokenHash = @TokenHash;

    IF @SessionId IS NULL OR @RevokedAt IS NOT NULL RETURN;

    DECLARE @Reason nvarchar(100) =
        CASE WHEN @NowUtc >= @ExpiresAt THEN N'AbsoluteTimeout'
             WHEN DATEDIFF(SECOND, @LastActivity, @NowUtc) > @IdleTimeoutMinutes * 60 THEN N'IdleTimeout'
             WHEN @UserActive = 0 THEN N'UserDisabled'
             WHEN @LockoutEnd IS NOT NULL AND @LockoutEnd > TODATETIMEOFFSET(@NowUtc, 0) THEN N'UserLocked'
        END;
    IF @Reason IS NOT NULL
    BEGIN
        UPDATE [identity].[Session] SET RevokedAtUtc = @NowUtc, RevokeReason = @Reason WHERE SessionId = @SessionId;
        RETURN;
    END

    UPDATE [identity].[Session] SET LastActivityUtc = @NowUtc WHERE SessionId = @SessionId;

    SELECT s.SessionId, s.UserId, u.UserName, u.DisplayName, u.AreaId, u.MustChangePassword, s.LastReauthUtc, s.ExpiresAtUtc,
           (SELECT STRING_AGG(r.Name, ',') FROM [identity].UserRole AS ur JOIN [identity].[Role] AS r ON r.RoleId = ur.RoleId
             WHERE ur.UserId = u.UserId) AS Roles
    FROM [identity].[Session] AS s JOIN [identity].[User] AS u ON u.UserId = s.UserId
    WHERE s.SessionId = @SessionId;
END
GO

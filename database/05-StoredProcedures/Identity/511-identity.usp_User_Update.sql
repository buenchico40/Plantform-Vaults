-- PlatformVault · Actualización de usuario con control de concurrencia
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE [identity].usp_User_Update
    @UserId uniqueidentifier, @UserName nvarchar(100), @NormalizedUserName nvarchar(100),
    @Email nvarchar(256) = NULL, @NormalizedEmail nvarchar(256) = NULL, @DisplayName nvarchar(200),
    @PasswordHash nvarchar(512) = NULL, @SecurityStamp nvarchar(100), @ExpectedConcurrencyStamp nvarchar(100),
    @NewConcurrencyStamp nvarchar(100), @LockoutEnabled bit, @LockoutEndUtc datetimeoffset(3) = NULL,
    @AccessFailedCount int, @IsActive bit, @AreaId uniqueidentifier = NULL, @ManagerUserId uniqueidentifier = NULL,
    @PasswordChangedAtUtc datetime2(3) = NULL, @MustChangePassword bit, @ModifiedBy uniqueidentifier = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    UPDATE [identity].[User]
       SET UserName = @UserName, NormalizedUserName = @NormalizedUserName, Email = @Email, NormalizedEmail = @NormalizedEmail,
           DisplayName = @DisplayName, PasswordHash = @PasswordHash, SecurityStamp = @SecurityStamp,
           ConcurrencyStamp = @NewConcurrencyStamp, LockoutEnabled = @LockoutEnabled, LockoutEndUtc = @LockoutEndUtc,
           AccessFailedCount = @AccessFailedCount, IsActive = @IsActive, AreaId = @AreaId, ManagerUserId = @ManagerUserId,
           PasswordChangedAtUtc = @PasswordChangedAtUtc, MustChangePassword = @MustChangePassword,
           ModifiedAtUtc = sysutcdatetime(), ModifiedBy = @ModifiedBy
     WHERE UserId = @UserId AND ConcurrencyStamp = @ExpectedConcurrencyStamp;
    IF @@ROWCOUNT = 0 THROW 51001, N'CONCURRENCY_CONFLICT', 1;
END
GO

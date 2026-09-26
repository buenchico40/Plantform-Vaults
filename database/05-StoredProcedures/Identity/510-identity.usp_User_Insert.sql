-- PlatformVault · Alta de usuario local (ASP.NET Core Identity)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE [identity].usp_User_Insert
    @UserId uniqueidentifier, @UserName nvarchar(100), @NormalizedUserName nvarchar(100),
    @Email nvarchar(256) = NULL, @NormalizedEmail nvarchar(256) = NULL, @DisplayName nvarchar(200),
    @PasswordHash nvarchar(512) = NULL, @SecurityStamp nvarchar(100), @ConcurrencyStamp nvarchar(100),
    @LockoutEnabled bit, @LockoutEndUtc datetimeoffset(3) = NULL, @AccessFailedCount int = 0, @IsActive bit,
    @AreaId uniqueidentifier = NULL, @ManagerUserId uniqueidentifier = NULL,
    @PasswordChangedAtUtc datetime2(3) = NULL, @MustChangePassword bit, @CreatedBy uniqueidentifier = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    INSERT [identity].[User] (UserId, UserName, NormalizedUserName, Email, NormalizedEmail, DisplayName, PasswordHash,
        SecurityStamp, ConcurrencyStamp, LockoutEnabled, LockoutEndUtc, AccessFailedCount, IsActive, AreaId, ManagerUserId,
        PasswordChangedAtUtc, MustChangePassword, CreatedBy)
    VALUES (@UserId, @UserName, @NormalizedUserName, @Email, @NormalizedEmail, @DisplayName, @PasswordHash,
        @SecurityStamp, @ConcurrencyStamp, @LockoutEnabled, @LockoutEndUtc, @AccessFailedCount, @IsActive, @AreaId, @ManagerUserId,
        @PasswordChangedAtUtc, @MustChangePassword, @CreatedBy);
END
GO

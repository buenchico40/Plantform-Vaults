-- PlatformVault · Validación de unicidad de nombre y huella (RN-002, RN-003)
SET NOCOUNT ON;
GO
CREATE OR ALTER PROCEDURE app.usp_ManagedObject_ExistsDuplicate
    @ObjectType varchar(20), @Environment varchar(20), @AreaId uniqueidentifier, @Name nvarchar(200),
    @Thumbprint char(64) = NULL, @ExcludeObjectId uniqueidentifier = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        CAST(CASE WHEN EXISTS (SELECT 1 FROM app.ManagedObject WHERE ObjectType = @ObjectType AND Environment = @Environment
             AND AreaId = @AreaId AND Name = @Name AND (@ExcludeObjectId IS NULL OR ObjectId <> @ExcludeObjectId)) THEN 1 ELSE 0 END AS bit) AS NameExists,
        CAST(CASE WHEN @Thumbprint IS NOT NULL AND EXISTS (SELECT 1 FROM app.ManagedObject WHERE Thumbprint = @Thumbprint
             AND LifecycleState <> 'Deactivated' AND (@ExcludeObjectId IS NULL OR ObjectId <> @ExcludeObjectId)) THEN 1 ELSE 0 END AS bit) AS ThumbprintExists;
END
GO

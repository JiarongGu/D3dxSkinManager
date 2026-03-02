namespace D3dxSkinManager.Modules.Migration.Models;

/// <summary>
/// Constants for migration error codes, steps, and categories
/// Frontend will map these to i18n keys
/// </summary>
public static class MigrationErrorCodes
{
    /// <summary>
    /// Error message codes
    /// Mapped to i18n keys: migration.error.code.{camelCase}
    /// </summary>
    public static class Messages
    {
        public const string MOD_MIGRATION_FAILED = "MOD_MIGRATION_FAILED";
        public const string PREVIEW_COPY_FAILED = "PREVIEW_COPY_FAILED";
        public const string ARCHIVE_COPY_FAILED = "ARCHIVE_COPY_FAILED";
        public const string CATEGORY_MIGRATION_FAILED = "CATEGORY_MIGRATION_FAILED";
        public const string CONFIG_MIGRATION_FAILED = "CONFIG_MIGRATION_FAILED";
        public const string METADATA_PARSE_FAILED = "METADATA_PARSE_FAILED";
        public const string FILE_NOT_FOUND = "FILE_NOT_FOUND";
        public const string INVALID_ARCHIVE = "INVALID_ARCHIVE";
        public const string PERMISSION_DENIED = "PERMISSION_DENIED";
    }

    /// <summary>
    /// Step codes where errors occurred
    /// Mapped to i18n keys: migration.error.step.{camelCase}
    /// </summary>
    public static class Steps
    {
        public const string ANALYZE_SOURCE = "ANALYZE_SOURCE";
        public const string MIGRATE_MOD_ARCHIVES = "MIGRATE_MOD_ARCHIVES";
        public const string MIGRATE_MOD_PREVIEWS = "MIGRATE_MOD_PREVIEWS";
        public const string MIGRATE_CATEGORIES = "MIGRATE_CATEGORIES";
        public const string MIGRATE_CONFIGURATION = "MIGRATE_CONFIGURATION";
        public const string CLEANUP = "CLEANUP";
    }

    /// <summary>
    /// Category codes for error classification
    /// Mapped to i18n keys: migration.error.category.{camelCase}
    /// </summary>
    public static class Categories
    {
        public const string MOD_MIGRATION = "MOD_MIGRATION";
        public const string PREVIEW_MIGRATION = "PREVIEW_MIGRATION";
        public const string CATEGORY_MIGRATION = "CATEGORY_MIGRATION";
        public const string CONFIG_MIGRATION = "CONFIG_MIGRATION";
        public const string SYSTEM = "SYSTEM";
        public const string FILE_OPERATION = "FILE_OPERATION";
    }
}

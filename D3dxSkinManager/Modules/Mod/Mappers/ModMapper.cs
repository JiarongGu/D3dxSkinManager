using D3dxSkinManager.Modules.Core.Utilities;
using D3dxSkinManager.Modules.Mod.Entities;
using D3dxSkinManager.Modules.Mod.Models;

namespace D3dxSkinManager.Modules.Mod.Mappers;

/// <summary>
/// Maps between ModEntity (database) and ModInfo (domain model)
/// </summary>
public static class ModMapper
{
    /// <summary>
    /// Convert database entity to domain model (basic properties only)
    /// Computed properties (IsLoaded, CategoryName, etc.) must be set by services
    /// </summary>
    /// <param name="entity">Database entity</param>
    /// <returns>Domain model with basic properties populated</returns>
    public static ModInfo ToDomain(ModEntity entity)
    {
        // Parse tags with error handling
        List<string> tags;
        try
        {
            tags = string.IsNullOrEmpty(entity.Tags)
                ? new List<string>()
                : JsonHelper.Deserialize<List<string>>(entity.Tags) ?? new List<string>();
        }
        catch
        {
            // Invalid JSON or wrong type - return empty list for graceful degradation
            tags = new List<string>();
        }

        return new ModInfo
        {
            // Core properties from database
            Id = entity.Id,
            Category = entity.Category,
            Name = entity.Name,
            // Impedance mismatch: Persistence layer (nullable) → Domain layer (non-nullable)
            // Database is flexible and compatible (allows NULL), domain is strict (never null)
            Author = entity.Author ?? string.Empty,
            Description = entity.Description ?? string.Empty,
            Type = entity.Type,
            Grading = entity.Grading,
            Tags = tags,
            DisablePreview = entity.DisablePreview,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            Metadata = entity.Metadata ?? string.Empty,
            RemoteLibraryId = entity.RemoteLibraryId,

            // Computed properties - initialized to defaults
            // Services should populate these after conversion
            CategoryName = string.Empty,
            LibraryName = string.Empty,
            TagsWithMetadata = new List<Tag>(),
            IsLoaded = false,
            IsAvailable = false,
            HasCache = false,
            HasPreviewFolder = false,
            IsOrphaned = false,
            CachePath = null,
            PreviewFolderPath = null,
            ArchiveFolderPath = null
        };
    }

    /// <summary>
    /// Convert domain model to database entity (for inserts)
    /// Computed properties are ignored - they don't belong in the database
    /// </summary>
    /// <param name="domainModel">Domain model</param>
    /// <returns>Database entity for storage</returns>
    public static ModEntity ToEntity(ModInfo domainModel)
    {
        return new ModEntity
        {
            Id = domainModel.Id,
            Category = domainModel.Category,
            Name = domainModel.Name,
            Author = domainModel.Author,
            Description = domainModel.Description,
            Type = domainModel.Type,
            Grading = domainModel.Grading,
            Tags = domainModel.Tags == null ? "[]" : JsonHelper.Serialize(domainModel.Tags),
            DisablePreview = domainModel.DisablePreview,
            CreatedAt = domainModel.CreatedAt,
            UpdatedAt = domainModel.UpdatedAt,
            Metadata = domainModel.Metadata,
            RemoteLibraryId = domainModel.RemoteLibraryId

            // Note: Computed properties (IsLoaded, CategoryName, LibraryName, file paths) are NOT stored
        };
    }

    /// <summary>
    /// Update existing entity from domain model (for updates)
    /// Does NOT update Id (primary key) or timestamps
    /// </summary>
    /// <param name="entity">Existing entity to update</param>
    /// <param name="domainModel">Domain model with new values</param>
    public static void UpdateEntity(ModEntity entity, ModInfo domainModel)
    {
        // Do NOT update Id (primary key)
        entity.Category = domainModel.Category;
        entity.Name = domainModel.Name;
        entity.Author = domainModel.Author;
        entity.Description = domainModel.Description;
        entity.Type = domainModel.Type;
        entity.Grading = domainModel.Grading;
        entity.Tags = domainModel.Tags == null ? "[]" : JsonHelper.Serialize(domainModel.Tags);
        entity.DisablePreview = domainModel.DisablePreview;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.Metadata = domainModel.Metadata;
        entity.RemoteLibraryId = domainModel.RemoteLibraryId;

        // Note: CreatedAt is NOT updated
        // Note: Computed properties are ignored
    }

    /// <summary>
    /// Convert multiple entities to domain models (batch operation)
    /// </summary>
    /// <param name="entities">List of entities</param>
    /// <returns>List of domain models</returns>
    public static List<ModInfo> ToDomainList(List<ModEntity> entities)
    {
        return entities.Select(ToDomain).ToList();
    }
}

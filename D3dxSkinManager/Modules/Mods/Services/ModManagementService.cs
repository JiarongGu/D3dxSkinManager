using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Mods.Models;

namespace D3dxSkinManager.Modules.Mods.Services;

/// <summary>
/// Interface for centralized mod management operations
/// </summary>
public interface IModManagementService
{
    Task<ModInfo> CreateModAsync(CreateModRequest request);
    Task<ModInfo> UpdateModAsync(string sha, UpdateModRequest request);
    Task<bool> DeleteModAsync(string sha);
    Task<ModInfo?> GetOrCreateModAsync(string sha, CreateModRequest request);
}

/// <summary>
/// Request model for creating a new mod
/// </summary>
public class CreateModRequest
{
    public required string SHA { get; set; }
    public required string Category { get; set; }
    public required string Name { get; set; }
    public string? Author { get; set; }
    public string? Description { get; set; }
    public string Type { get; set; } = "zip";
    public string Grading { get; set; } = "G";
    public List<string> Tags { get; set; } = new();
    public bool IsLoaded { get; set; } = false;
    public bool IsAvailable { get; set; } = true;
    public string? ThumbnailPath { get; set; }
    // Note: Preview paths are dynamically scanned from previews/{SHA}/ folder
}

/// <summary>
/// Request model for updating an existing mod
/// </summary>
public class UpdateModRequest
{
    public string? Category { get; set; }
    public string? Name { get; set; }
    public string? Author { get; set; }
    public string? Description { get; set; }
    public string? Type { get; set; }
    public string? Grading { get; set; }
    public List<string>? Tags { get; set; }
    public bool? IsLoaded { get; set; }
    public bool? IsAvailable { get; set; }
    public string? ThumbnailPath { get; set; }
    // Note: Preview paths are dynamically scanned from previews/{SHA}/ folder
}

/// <summary>
/// Centralized service for mod creation, update, and deletion operations
/// Provides consistent mod management logic across migration, import, and other operations
/// </summary>
public class ModManagementService : IModManagementService
{
    private readonly IModRepository _repository;
    private readonly ILogHelper _logger;

    public ModManagementService(IModRepository repository, ILogHelper logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Create a new mod with validation and default values
    /// </summary>
    public async Task<ModInfo> CreateModAsync(CreateModRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SHA))
            throw new ArgumentException("SHA is required", nameof(request.SHA));

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Name is required", nameof(request.Name));

        // Check if already exists
        if (await _repository.ExistsAsync(request.SHA))
        {
            throw new InvalidOperationException($"Mod with SHA {request.SHA} already exists");
        }

        var mod = new ModInfo
        {
            SHA = request.SHA,
            Category = request.Category ?? string.Empty,
            Name = request.Name,
            Author = request.Author ?? string.Empty,
            Description = request.Description ?? string.Empty,
            Type = request.Type,
            Grading = request.Grading,
            Tags = request.Tags ?? new List<string>(),
            IsLoaded = request.IsLoaded,
            IsAvailable = request.IsAvailable,
            ThumbnailPath = request.ThumbnailPath
            // Note: Preview paths are dynamically scanned from previews/{SHA}/ folder
        };

        await _repository.InsertAsync(mod);
        _logger.Info($"Created mod: {mod.Name} ({mod.SHA})", "ModManagementService");

        return mod;
    }

    /// <summary>
    /// Update an existing mod with partial updates (only specified fields)
    /// </summary>
    public async Task<ModInfo> UpdateModAsync(string sha, UpdateModRequest request)
    {
        if (string.IsNullOrWhiteSpace(sha))
            throw new ArgumentException("SHA is required", nameof(sha));

        var mod = await _repository.GetByIdAsync(sha);
        if (mod == null)
        {
            throw new InvalidOperationException($"Mod with SHA {sha} not found");
        }

        // Apply updates only for non-null values
        if (request.Category != null) mod.Category = request.Category;
        if (request.Name != null) mod.Name = request.Name;
        if (request.Author != null) mod.Author = request.Author;
        if (request.Description != null) mod.Description = request.Description;
        if (request.Type != null) mod.Type = request.Type;
        if (request.Grading != null) mod.Grading = request.Grading;
        if (request.Tags != null) mod.Tags = request.Tags;
        if (request.IsLoaded.HasValue) mod.IsLoaded = request.IsLoaded.Value;
        if (request.IsAvailable.HasValue) mod.IsAvailable = request.IsAvailable.Value;
        if (request.ThumbnailPath != null) mod.ThumbnailPath = request.ThumbnailPath;
        // Note: Preview paths are dynamically scanned from previews/{SHA}/ folder

        await _repository.UpdateAsync(mod);
        _logger.Info($"Updated mod: {mod.Name} ({sha})", "ModManagementService");

        return mod;
    }

    /// <summary>
    /// Delete a mod by SHA
    /// </summary>
    public async Task<bool> DeleteModAsync(string sha)
    {
        if (string.IsNullOrWhiteSpace(sha))
            throw new ArgumentException("SHA is required", nameof(sha));

        var exists = await _repository.ExistsAsync(sha);
        if (!exists)
        {
            _logger.Warning($"Mod not found for deletion: {sha}", "ModManagementService");
            return false;
        }

        var success = await _repository.DeleteAsync(sha);
        if (success)
        {
            _logger.Info($"Deleted mod: {sha}", "ModManagementService");
        }

        return success;
    }

    /// <summary>
    /// Get existing mod or create if it doesn't exist
    /// Useful for idempotent operations like migration
    /// </summary>
    public async Task<ModInfo?> GetOrCreateModAsync(string sha, CreateModRequest request)
    {
        if (string.IsNullOrWhiteSpace(sha))
            throw new ArgumentException("SHA is required", nameof(sha));

        // Try to get existing mod
        var existing = await _repository.GetByIdAsync(sha);
        if (existing != null)
        {
            _logger.Debug($"Mod already exists: {existing.Name} ({sha})", "ModManagementService");
            return existing;
        }

        // Create new mod
        return await CreateModAsync(request);
    }
}

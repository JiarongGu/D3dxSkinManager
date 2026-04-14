import { BaseModuleService } from "../baseModuleService";
import {
  ModInfo,
  ModLoadResult,
  Tag,
  ModKeybinding,
  UpdateModMetadataRequest,
  ModStatistics,
  BatchDeleteResult,
  ModPresetInfo,
  ModPresetApplyResult,
} from "../../types/mod.types";
import type { ModIpcRequests } from "../../types/ipc/modIpcRequests";

// Re-export types for backwards compatibility
export type {
  ModInfo,
  ModLoadResult,
  Tag,
  ModKeybinding,
  UpdateModMetadataRequest,
  ModStatistics,
  BatchDeleteResult,
  ModPresetInfo,
  ModPresetApplyResult,
};
export type { ModIpcRequests };

/**
 * Service for mod management operations
 * Provides type-safe communication with the MOD module backend
 */
export class ModService extends BaseModuleService {
  constructor() {
    super("MOD");
  }

  /**
   * Get all available mods (not used in category-based workflow, kept for api reference)
   */
  async getAllMods(profileId: string): Promise<ModInfo[]> {
    return this.sendTypedArray<ModIpcRequests, ModInfo>("GET_ALL", profileId);
  }

  /**
   * Load a mod by ID
   * Returns affected mod IDs for efficient frontend updates (avoids full list refresh)
   */
  async loadMod(profileId: string, id: string): Promise<ModLoadResult> {
    return this.sendTypedMessage<ModIpcRequests, ModLoadResult>(
      "LOAD",
      profileId,
      { id },
    );
  }

  /**
   * Unload a mod by ID
   */
  async unloadMod(profileId: string, id: string): Promise<boolean> {
    return this.sendTypedBoolean<ModIpcRequests>("UNLOAD", profileId, { id });
  }

  /**
   * Get list of loaded mod IDs
   */
  async getLoadedMods(profileId: string): Promise<string[]> {
    return this.sendTypedArray<ModIpcRequests, string>("GET_LOADED", profileId);
  }

  /**
   * Import a mod from file path
   */
  async importMod(profileId: string, filePath: string): Promise<ModInfo> {
    return this.sendTypedMessage<ModIpcRequests, ModInfo>("IMPORT", profileId, {
      filePath,
    });
  }

  /**
   * Delete a mod permanently
   */
  async deleteMod(profileId: string, id: string): Promise<boolean> {
    return this.sendTypedBoolean<ModIpcRequests>("DELETE", profileId, { id });
  }

  /**
   * Delete mod cache (both active and disabled cache folders)
   */
  async deleteCache(profileId: string, id: string): Promise<boolean> {
    return this.sendTypedBoolean<ModIpcRequests>("DELETE_CACHE", profileId, {
      id,
    });
  }

  /**
   * Update mod archive from cache folder (re-compress cache back to archive)
   */
  async updateArchiveFromCache(profileId: string, id: string): Promise<boolean> {
    return this.sendTypedBoolean<ModIpcRequests>(
      "UPDATE_ARCHIVE_FROM_CACHE",
      profileId,
      { id },
    );
  }

  /**
   * Batch delete mods permanently (cache, preview, archive, database)
   */
  async batchDeleteMods(
    profileId: string,
    ids: string[],
  ): Promise<BatchDeleteResult> {
    return this.sendTypedMessage<ModIpcRequests, BatchDeleteResult>(
      "BATCH_DELETE",
      profileId,
      { ids },
    );
  }

  /**
   * Batch delete mod caches (both active and disabled cache folders)
   */
  async batchDeleteCaches(
    profileId: string,
    ids: string[],
  ): Promise<BatchDeleteResult> {
    return this.sendTypedMessage<ModIpcRequests, BatchDeleteResult>(
      "BATCH_DELETE_CACHES",
      profileId,
      { ids },
    );
  }

  /**
   * Export a mod to a file
   */
  async exportMod(
    profileId: string,
    id: string,
    targetPath: string,
  ): Promise<boolean> {
    return this.sendTypedBoolean<ModIpcRequests>("EXPORT", profileId, {
      id,
      targetPath,
    });
  }

  /**
   * Get mods by Category node ID
   */
  async getModsByCategory(
    profileId: string,
    categoryId: string,
  ): Promise<ModInfo[]> {
    return this.sendTypedArray<ModIpcRequests, ModInfo>(
      "GET_MODS_BY_CATEGORY",
      profileId,
      { categoryId },
    );
  }

  /**
   * Get all mods that don't have any Category tags
   */
  async getUnclassifiedMods(profileId: string): Promise<ModInfo[]> {
    return this.sendTypedArray<ModIpcRequests, ModInfo>(
      "GET_UNCLASSIFIED_MODS",
      profileId,
    );
  }

  /**
   * Get count of mods that don't have any Category tags
   */
  async getUnclassifiedCount(profileId: string): Promise<number> {
    return this.sendTypedMessage<ModIpcRequests, number>(
      "GET_UNCLASSIFIED_COUNT",
      profileId,
    );
  }

  /**
   * Get active mods by scanning cache folder first, then matching with database
   * Returns mods that are currently active in cache (not DISABLED-), including orphaned ones not in DB
   * Orphaned mods have IsOrphaned flag set to true for frontend to handle i18n display
   */
  async getActiveMods(profileId: string): Promise<ModInfo[]> {
    return this.sendTypedArray<ModIpcRequests, ModInfo>(
      "GET_ACTIVE_MODS",
      profileId,
    );
  }

  /**
   * Get unique authors
   */
  async getAuthors(profileId: string): Promise<string[]> {
    return this.sendTypedArray<ModIpcRequests, string>(
      "GET_AUTHORS",
      profileId,
    );
  }

  /**
   * Get mod statistics (total mods, loaded mods, etc.)
   */
  async getStatistics(profileId: string): Promise<ModStatistics> {
    return this.sendTypedMessage<ModIpcRequests, ModStatistics>(
      "GET_STATISTICS",
      profileId,
    );
  }

  /**
   * Get all unique tag names actually used in mods (from Mods.Tags column)
   * For backward compatibility - use getAllTags() for Tag objects with colors
   */
  async getTags(profileId: string): Promise<string[]> {
    return this.sendTypedArray<ModIpcRequests, string>("GET_TAGS", profileId);
  }

  // ============= Tag Management (Tags Table) =============

  /**
   * Get all tags from Tags table (master tag definitions with colors)
   */
  async getAllTags(profileId: string): Promise<Tag[]> {
    return this.sendTypedArray<ModIpcRequests, Tag>("GET_ALL_TAGS", profileId);
  }

  /**
   * Get a specific tag by name from Tags table
   */
  async getTagByName(
    profileId: string,
    name: string,
  ): Promise<Tag | undefined> {
    return this.sendTypedOptional<ModIpcRequests, Tag>(
      "GET_TAG_BY_NAME",
      profileId,
      { name },
    );
  }

  /**
   * Create or update a tag in Tags table
   */
  async upsertTag(
    profileId: string,
    name: string,
    color: string,
  ): Promise<boolean> {
    return this.sendTypedBoolean<ModIpcRequests>("UPSERT_TAG", profileId, {
      name,
      color,
    });
  }

  /**
   * Delete a tag from Tags table (doesn't affect mod.tags, only removes from autocomplete)
   */
  async deleteTag(profileId: string, name: string): Promise<boolean> {
    return this.sendTypedBoolean<ModIpcRequests>("DELETE_TAG", profileId, {
      name,
    });
  }

  /**
   * Get all unique tag names that are actually used in mods
   */
  async getUsedTagNames(profileId: string): Promise<string[]> {
    return this.sendTypedArray<ModIpcRequests, string>(
      "GET_USED_TAG_NAMES",
      profileId,
    );
  }

  /**
   * Get the number of mods using a specific tag
   */
  async getTagUsageCount(profileId: string, tag: string): Promise<number> {
    return this.sendTypedMessage<ModIpcRequests, number>(
      "GET_TAG_USAGE_COUNT",
      profileId,
      { tag },
    );
  }

  /**
   * Search tags by name (case-insensitive substring match)
   * Returns full Tag objects with colors
   */
  async searchTags(profileId: string, searchTerm: string): Promise<Tag[]> {
    return this.sendTypedArray<ModIpcRequests, Tag>("SEARCH_TAGS", profileId, {
      searchTerm,
    });
  }

  /**
   * Search mods by keyword (supports ! for negation, space-separated for AND)
   */
  async searchMods(profileId: string, searchTerm: string): Promise<ModInfo[]> {
    return this.sendTypedArray<ModIpcRequests, ModInfo>("SEARCH", profileId, {
      searchTerm,
    });
  }

  /**
   * Get mod by ID
   */
  async getModById(
    profileId: string,
    id: string,
  ): Promise<ModInfo | undefined> {
    return this.sendTypedOptional<ModIpcRequests, ModInfo>(
      "GET_BY_ID",
      profileId,
      { id },
    );
  }

  /**
   * Update mod metadata
   */
  async updateMetadata(
    profileId: string,
    id: string,
    metadata: UpdateModMetadataRequest,
  ): Promise<boolean> {
    return this.sendTypedBoolean<ModIpcRequests>("UPDATE_METADATA", profileId, {
      id,
      ...metadata,
    });
  }

  /**
   * Update mod category (Category)
   */
  async updateCategory(
    profileId: string,
    id: string,
    category: string,
  ): Promise<boolean> {
    return this.sendTypedBoolean<ModIpcRequests>("UPDATE_CATEGORY", profileId, {
      id,
      category,
    });
  }

  /**
   * Batch update category for multiple mods with individual values for each mod
   */
  async batchUpdateCategory(
    profileId: string,
    updates: Record<string, string>,
  ): Promise<number> {
    return this.sendTypedMessage<ModIpcRequests, number>(
      "BATCH_UPDATE_CATEGORY",
      profileId,
      {
        updates,
      },
    );
  }

  /**
   * Batch update metadata for multiple mods with individual values for each mod
   */
  async batchUpdateMetadata(
    profileId: string,
    updates: Record<string, UpdateModMetadataRequest>,
  ): Promise<{ updatedCount: number; totalRequested: number }> {
    return this.sendTypedMessage<
      ModIpcRequests,
      { updatedCount: number; totalRequested: number }
    >("BATCH_UPDATE_METADATA", profileId, {
      updates,
    });
  }

  /**
   * Get preview paths for a mod
   */
  async getPreviewPaths(profileId: string, id: string): Promise<string[]> {
    return this.sendTypedArray<ModIpcRequests, string>(
      "GET_PREVIEW_PATHS",
      profileId,
      { id },
    );
  }

  /**
   * Import a preview image for a mod
   */
  async importPreviewImage(
    profileId: string,
    id: string,
    imagePath: string,
  ): Promise<boolean> {
    const result = await this.sendTypedMessage<
      ModIpcRequests,
      { success: boolean; message: string }
    >("IMPORT_PREVIEW_IMAGE", profileId, {
      id,
      imagePath,
    });
    return result.success;
  }

  /**
   * Check if clipboard contains an image
   */
  async checkClipboardHasImage(profileId: string): Promise<boolean> {
    return this.sendTypedBoolean<ModIpcRequests>(
      "CHECK_CLIPBOARD_HAS_IMAGE",
      profileId,
    );
  }

  /**
   * Import a preview image from clipboard for a mod
   */
  async importPreviewFromClipboard(
    profileId: string,
    id: string,
  ): Promise<boolean> {
    const result = await this.sendTypedMessage<
      ModIpcRequests,
      { success: boolean; message: string }
    >("IMPORT_PREVIEW_FROM_CLIPBOARD", profileId, {
      id,
    });
    return result.success;
  }

  /**
   * Copy a preview image to clipboard
   */
  async copyPreviewToClipboard(
    profileId: string,
    previewPath: string,
  ): Promise<boolean> {
    const result = await this.sendTypedMessage<
      ModIpcRequests,
      { success: boolean; message: string }
    >("COPY_PREVIEW_TO_CLIPBOARD", profileId, {
      previewPath,
    });
    return result.success;
  }

  /**
   * Set a preview image as the mod thumbnail
   */
  async setThumbnail(
    profileId: string,
    id: string,
    previewPath: string,
  ): Promise<boolean> {
    const result = await this.sendTypedMessage<
      ModIpcRequests,
      { success: boolean; message: string }
    >("SET_THUMBNAIL", profileId, {
      id,
      previewPath,
    });
    return result.success;
  }

  /**
   * Delete a preview image
   */
  async deletePreview(
    profileId: string,
    id: string,
    previewPath: string,
  ): Promise<boolean> {
    const result = await this.sendTypedMessage<
      ModIpcRequests,
      { success: boolean; message: string }
    >("DELETE_PREVIEW", profileId, {
      id,
      previewPath,
    });
    return result.success;
  }

  /**
   * Check file paths for a mod (on-demand for context menu)
   * Returns paths only if they exist on the file system
   */
  async checkFilePaths(
    profileId: string,
    id: string,
  ): Promise<{
    originalPath: string | undefined;
    cachePath: string | undefined;
    thumbnailPath: string | undefined;
  }> {
    return this.sendTypedMessage<
      ModIpcRequests,
      {
        originalPath: string | undefined;
        cachePath: string | undefined;
        thumbnailPath: string | undefined;
      }
    >("CHECK_FILE_PATHS", profileId, { id });
  }

  /**
   * Get file paths for a mod (helper for file viewing operations)
   * Note: This is a client-side helper that constructs expected paths
   * The actual existence of these paths should be verified by the backend
   */
  getModFilePaths(mod: ModInfo): {
    originalFile?: string;
    cacheDirectory?: string;
  } {
    // Note: These are placeholder paths based on expected mod structure
    // In a real implementation, these would come from mod metadata or backend
    // For now, return undefined as backend will handle path resolution
    return {
      originalFile: mod.originalPath,
      cacheDirectory: mod.cachePath,
    };
  }

  /**
   * Get keybindings for a mod (parsed from .ini files in mod's work directory)
   */
  async getKeybindings(
    profileId: string,
    id: string,
  ): Promise<ModKeybinding[]> {
    return this.sendTypedArray<ModIpcRequests, ModKeybinding>(
      "GET_KEYBINDINGS",
      profileId,
      { id },
    );
  }

  // ============= Preset Operations =============

  /**
   * Get all saved mod presets
   */
  async getPresets(profileId: string): Promise<ModPresetInfo[]> {
    return this.sendTypedArray<ModIpcRequests, ModPresetInfo>(
      "GET_PRESETS",
      profileId,
    );
  }

  /**
   * Save currently active mods as a new preset
   */
  async savePreset(profileId: string, name: string): Promise<ModPresetInfo> {
    return this.sendTypedMessage<ModIpcRequests, ModPresetInfo>(
      "SAVE_PRESET",
      profileId,
      { name },
    );
  }

  /**
   * Update a preset's name
   */
  async updatePreset(
    profileId: string,
    id: string,
    name: string,
  ): Promise<ModPresetInfo> {
    return this.sendTypedMessage<ModIpcRequests, ModPresetInfo>(
      "UPDATE_PRESET",
      profileId,
      { id, name },
    );
  }

  /**
   * Delete a preset
   */
  async deletePreset(profileId: string, id: string): Promise<boolean> {
    return this.sendTypedBoolean<ModIpcRequests>(
      "DELETE_PRESET",
      profileId,
      { id },
    );
  }

  /**
   * Apply a preset: unload current mods and load the preset's mods
   */
  async applyPreset(
    profileId: string,
    id: string,
  ): Promise<ModPresetApplyResult> {
    return this.sendTypedMessage<ModIpcRequests, ModPresetApplyResult>(
      "APPLY_PRESET",
      profileId,
      { id },
    );
  }

  /**
   * Unload all currently loaded mods
   */
  async unloadAllMods(profileId: string): Promise<boolean> {
    return this.sendTypedBoolean<ModIpcRequests>(
      "UNLOAD_ALL_MODS",
      profileId,
    );
  }
}

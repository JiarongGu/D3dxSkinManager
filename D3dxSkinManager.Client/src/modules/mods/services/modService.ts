import { BaseModuleService } from '../../../shared/services/baseModuleService';
import { ModInfo, ModLoadResult } from '../../../shared/types/mod.types';

// Re-export types for backwards compatibility
export type { ModInfo, ModLoadResult };

/**
 * Service for mod management operations
 * Provides type-safe communication with the MOD module backend
 */
export class ModService extends BaseModuleService {
  constructor() {
    super('MOD');
  }

  /**
   * Get all available mods
   */
  async getAllMods(profileId: string): Promise<ModInfo[]> {
    return this.sendArrayMessage<ModInfo>('GET_ALL', profileId);
  }

  /**
   * Load a mod by SHA
   * Returns affected mod SHAs for efficient frontend updates (avoids full list refresh)
   */
  async loadMod(profileId: string, sha: string): Promise<ModLoadResult> {
    return this.sendMessage<ModLoadResult>('LOAD', profileId, { sha });
  }

  /**
   * Unload a mod by SHA
   */
  async unloadMod(profileId: string, sha: string): Promise<boolean> {
    return this.sendBooleanMessage('UNLOAD', profileId, { sha });
  }

  /**
   * Get list of loaded mod SHAs
   */
  async getLoadedMods(profileId: string): Promise<string[]> {
    return this.sendArrayMessage<string>('GET_LOADED', profileId);
  }

  /**
   * Import a mod from file path
   */
  async importMod(profileId: string, filePath: string): Promise<ModInfo> {
    return this.sendMessage<ModInfo>('IMPORT', profileId, { filePath });
  }

  /**
   * Delete a mod permanently
   */
  async deleteMod(profileId: string, sha: string): Promise<boolean> {
    return this.sendBooleanMessage('DELETE', profileId, { sha });
  }

  /**
   * Export a mod to a file
   */
  async exportMod(profileId: string, sha: string, targetPath: string): Promise<boolean> {
    return this.sendBooleanMessage('EXPORT', profileId, { sha, targetPath });
  }

  /**
   * Get mods by category
   */
  async getModsByObject(profileId: string, category: string): Promise<ModInfo[]> {
    return this.sendArrayMessage<ModInfo>('GET_BY_OBJECT', profileId, { category });
  }

  /**
   * Get mods by classification node ID
   */
  async getModsByClassification(profileId: string, classificationNodeId: string): Promise<ModInfo[]> {
    return this.sendArrayMessage<ModInfo>('GET_MODS_BY_CLASSIFICATION', profileId, { classificationNodeId });
  }

  /**
   * Get all mods that don't have any classification tags
   */
  async getUnclassifiedMods(profileId: string): Promise<ModInfo[]> {
    return this.sendArrayMessage<ModInfo>('GET_UNCLASSIFIED_MODS', profileId);
  }

  /**
   * Get count of mods that don't have any classification tags
   */
  async getUnclassifiedCount(profileId: string): Promise<number> {
    return this.sendMessage<number>('GET_UNCLASSIFIED_COUNT', profileId);
  }

  /**
   * Get unique object names
   */
  async getObjectNames(profileId: string): Promise<string[]> {
    return this.sendArrayMessage<string>('GET_OBJECT_NAMES', profileId);
  }

  /**
   * Get unique authors
   */
  async getAuthors(profileId: string): Promise<string[]> {
    return this.sendArrayMessage<string>('GET_AUTHORS', profileId);
  }

  /**
   * Get all unique tags
   */
  async getTags(profileId: string): Promise<string[]> {
    return this.sendArrayMessage<string>('GET_TAGS', profileId);
  }

  /**
   * Search mods by keyword (supports ! for negation, space-separated for AND)
   */
  async searchMods(profileId: string, searchTerm: string): Promise<ModInfo[]> {
    return this.sendArrayMessage<ModInfo>('SEARCH', profileId, { searchTerm });
  }

  /**
   * Get mod by SHA
   */
  async getModBySha(profileId: string, sha: string): Promise<ModInfo | null> {
    return this.sendNullableMessage<ModInfo>('GET_BY_SHA', profileId, { sha });
  }

  /**
   * Update mod metadata
   */
  async updateMetadata(
    profileId: string,
    sha: string,
    metadata: {
      name?: string;
      author?: string;
      tags?: string[];
      grading?: string;
      description?: string;
    }
  ): Promise<boolean> {
    return this.sendBooleanMessage('UPDATE_METADATA', profileId, {
      sha,
      ...metadata
    });
  }

  /**
   * Update mod category (classification)
   */
  async updateCategory(
    profileId: string,
    sha: string,
    category: string
  ): Promise<boolean> {
    return this.sendBooleanMessage('UPDATE_CATEGORY', profileId, {
      sha,
      category
    });
  }

  /**
   * Batch update metadata for multiple mods
   */
  async batchUpdateMetadata(
    profileId: string,
    shas: string[],
    metadata: {
      name?: string;
      author?: string;
      tags?: string[];
      grading?: string;
      description?: string;
    },
    fieldMask: string[]
  ): Promise<{ updatedCount: number; totalRequested: number }> {
    return this.sendMessage<{ updatedCount: number; totalRequested: number }>(
      'BATCH_UPDATE_METADATA',
      profileId,
      {
        shas,
        ...metadata,
        fieldMask
      }
    );
  }

  /**
   * Get preview paths for a mod
   */
  async getPreviewPaths(profileId: string, sha: string): Promise<string[]> {
    return this.sendArrayMessage<string>('GET_PREVIEW_PATHS', profileId, { sha });
  }

  /**
   * Import a preview image for a mod
   */
  async importPreviewImage(profileId: string, sha: string, imagePath: string): Promise<boolean> {
    const result = await this.sendMessage<{ success: boolean; message: string }>(
      'IMPORT_PREVIEW_IMAGE',
      profileId,
      {
        sha,
        imagePath
      }
    );
    return result.success;
  }

  /**
   * Set a preview image as the mod thumbnail
   */
  async setThumbnail(profileId: string, sha: string, previewPath: string): Promise<boolean> {
    const result = await this.sendMessage<{ success: boolean; message: string }>(
      'SET_THUMBNAIL',
      profileId,
      {
        sha,
        previewPath
      }
    );
    return result.success;
  }

  /**
   * Delete a preview image
   */
  async deletePreview(profileId: string, sha: string, previewPath: string): Promise<boolean> {
    const result = await this.sendMessage<{ success: boolean; message: string }>(
      'DELETE_PREVIEW',
      profileId,
      {
        sha,
        previewPath
      }
    );
    return result.success;
  }

  /**
   * Check file paths for a mod (on-demand for context menu)
   * Returns paths only if they exist on the file system
   */
  async checkFilePaths(
    profileId: string,
    sha: string
  ): Promise<{
    originalPath: string | null;
    workPath: string | null;
    thumbnailPath: string | null;
  }> {
    return this.sendMessage<{
      originalPath: string | null;
      workPath: string | null;
      thumbnailPath: string | null;
    }>('CHECK_FILE_PATHS', profileId, { sha });
  }

  /**
   * Get file paths for a mod (helper for file viewing operations)
   * Note: This is a client-side helper that constructs expected paths
   * The actual existence of these paths should be verified by the backend
   */
  getModFilePaths(mod: ModInfo): {
    originalFile?: string;
    workDirectory?: string;
    cacheDirectory?: string;
  } {
    // Note: These are placeholder paths based on expected mod structure
    // In a real implementation, these would come from mod metadata or backend
    // For now, return undefined as backend will handle path resolution
    return {
      originalFile: mod.originalPath,
      workDirectory: mod.workPath,
      cacheDirectory: mod.cachePath,
    };
  }
}

export const modService = new ModService();

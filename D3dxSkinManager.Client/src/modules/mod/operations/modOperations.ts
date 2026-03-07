/**
 * Mod operations - CRUD operations for mods
 * Centralized business logic with consistent error handling and state updates
 */

import { debounce } from 'lodash-es';
import { useModsStore } from '../store/modsStore';
import { ModInfo } from '../../../shared/types/mod.types';
import { CATEGORY_IDS } from '../../../shared/types/category.types';
import { notification } from '../../../shared/utils/notification';
import { handleError } from '../../../shared/utils/errorHandler';
import { executeWithDelayedLoading } from '../../../shared/utils/delayedLoading';
import { api, modService } from '../../../shared/services/ipc';
import i18n from '../../../shared/services/i18n';

/**
 * Internal refresh implementation
 * Refreshes only the currently selected category view for efficiency
 * Also syncs selectedMod with updated data from backend
 */
async function _refreshMods(profileId: string): Promise<void> {
  const { selectedCategory, selectedMod, setSelectedMod } = useModsStore.getState();

  // If a category is selected, refresh only that category's mods
  // Otherwise, fetch fresh mod data to update selectedMod
  if (selectedCategory) {
    const { loadModsByCategory, loadUncategorizedMods } = await import('./categoryOperations');

    if (selectedCategory.id === CATEGORY_IDS.UNCLASSIFIED) {
      await loadUncategorizedMods(profileId);
    } else {
      await loadModsByCategory(profileId, selectedCategory.id);
    }
  } else if (selectedMod?.sha) {
    // No category filter active, but we need to update selectedMod with fresh data
    // Fetch just the selected mod instead of the entire mod list for efficiency
    try {
      const freshMod = await modService.getModBySha(profileId, selectedMod.sha);
      if (freshMod) {
        setSelectedMod(freshMod);
      } else {
        // Mod was deleted
        setSelectedMod(undefined);
      }
    } catch (error) {
      // Mod not found or error fetching, clear selection
      setSelectedMod(undefined);
    }
    return; // Early return since we already updated selectedMod
  }

  // After refreshing category mods, update selectedMod if it still exists
  // This ensures preview panel shows updated data (isLoaded, metadata, etc.)
  if (selectedMod?.sha) {
    const { mods } = useModsStore.getState();
    const modList = mods || [];
    const updatedMod = modList.find((m: ModInfo) => m.sha === selectedMod.sha);

    if (updatedMod) {
      // Update selectedMod with fresh data from backend
      setSelectedMod(updatedMod);
    } else {
      // Mod was deleted, clear selection
      setSelectedMod(undefined);
    }
  }
}

/**
 * Refresh mods from backend (debounced 10ms to prevent mass IPC hits)
 * Only refreshes the currently selected category view, not all mods
 */
export const refreshMods = debounce(_refreshMods, 20);

/**
 * Update mod metadata
 * Uses delayed loading (100ms) to avoid flicker for fast updates
 */
export async function updateMod(
  profileId: string,
  sha: string,
  data: Partial<ModInfo>
): Promise<void> {
  const { updateModLocal } = useModsStore.getState();
  const categoryChanged = data.category !== undefined;

  try {
    await executeWithDelayedLoading(
      async () => {
        // Update metadata (name, author, tags, grading, description, disablePreview)
        await modService.updateMetadata(profileId, sha, {
          name: data.name,
          author: data.author,
          tags: data.tags,
          grading: data.grading,
          description: data.description,
          disablePreview: data.disablePreview,
        });

        // Update category separately if it changed
        if (categoryChanged && data.category) {
          await modService.updateCategory(profileId, sha, data.category);
        }

        // Update local state (Zustand automatically updates mods)
        updateModLocal(sha, data);

        notification.success(i18n.t('mods.operations.updateSuccess'));
      },
      useModsStore.getState().setModLoading,
      200
    );
  } catch (error: unknown) {
    handleError(error);
    throw error;
  }
}

/**
 * Delete a mod
 * Uses delayed loading (100ms) to avoid flicker for fast deletes
 */
export async function deleteMod(profileId: string, sha: string): Promise<void> {
  const { removeMod } = useModsStore.getState();

  try {
    await executeWithDelayedLoading(
      async () => {
        await modService.deleteMod(profileId, sha);

        // Remove from local state
        removeMod(sha);

        notification.success(i18n.t('mods.operations.deleteSuccess'));
      },
      useModsStore.getState().setModLoading,
      200
    );
  } catch (error: unknown) {
    handleError(error);
    throw error;
  }
}

/**
 * Load mod statistics
 */
export async function loadStatistics(profileId: string): Promise<void> {
  try {
    const statistics = await api.mod.getStatistics(profileId);
    useModsStore.getState().setStatistics(statistics);
  } catch (error: unknown) {
    console.error('Failed to load mod statistics:', error);
  }
}

/**
 * Load preview paths for the currently selected mod
 * Updates previewPaths in store and busts browser cache
 */
export async function loadPreviewPaths(profileId: string, sha: string): Promise<void> {
  const { setPreviewLoading, setPreviewPaths, bustPreviewCache, selectedMod } = useModsStore.getState();

  // Check if preview is disabled for this mod
  if (selectedMod?.disablePreview) {
    // Clear previews when disabled
    setPreviewPaths([]);
    return;
  }

  try {
    await executeWithDelayedLoading(
      async () => {
        // Backend automatically imports from cache if no previews exist
        const paths = await api.mod.getPreviewPaths(profileId, sha);
        setPreviewPaths(paths);
        bustPreviewCache(); // Bust browser cache
      },
      setPreviewLoading,
      200
    );
  } catch (error: unknown) {
    // Clear previews on error
    console.error('Failed to load preview paths:', error);
    setPreviewPaths([]);
    bustPreviewCache();
  }
}

/**
 * Reload preview paths for the currently selected mod
 * Used when PREVIEW_IMPORTED, THUMBNAIL_UPDATED, or PREVIEW_DELETED events are received
 * Busts cache to force browser to reload images
 */
export async function reloadCurrentPreview(profileId: string): Promise<void> {
  const { selectedMod, bustPreviewCache } = useModsStore.getState();

  if (selectedMod?.sha) {
    // Bust cache to force browser to reload images (prevents showing cached old images)
    bustPreviewCache();
    await loadPreviewPaths(profileId, selectedMod.sha);
  }
}

/**
 * Load a mod in-game
 * Backend will fire MOD_LIST_UPDATED event which triggers refresh via ModProvider
 */
export async function loadMod(profileId: string, sha: string): Promise<void> {
  try {
    // Perform backend operation - returns affected mod SHAs
    await modService.loadMod(profileId, sha);
    notification.success(i18n.t('mods.operations.loadSuccess'));

    // Backend fires MOD_LIST_UPDATED event → ModProvider refreshes mods automatically
  } catch (error: unknown) {
    // Handle error with user-friendly messages
    handleError(error);
    throw error;
  }
}

/**
 * Unload a mod from game
 * Backend will fire MOD_LIST_UPDATED event which triggers refresh via ModProvider
 */
export async function unloadMod(profileId: string, sha: string): Promise<void> {
  try {
    // Perform backend operation
    await modService.unloadMod(profileId, sha);
    notification.success(i18n.t('mods.operations.unloadSuccess'));

    // Backend fires MOD_LIST_UPDATED event → ModProvider refreshes mods automatically
  } catch (error: unknown) {
    // Handle error with user-friendly messages
    handleError(error);
    throw error;
  }
}


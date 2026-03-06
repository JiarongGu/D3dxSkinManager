/**
 * Mod operations - CRUD operations for mods
 * Centralized business logic with consistent error handling and state updates
 */

import { debounce } from 'lodash-es';
import { useModsStore } from '../store/modsStore';
import { ModInfo } from '../../../shared/types/mod.types';
import { notification } from '../../../shared/utils/notification';
import { handleError } from '../../../shared/utils/errorHandler';
import { executeWithDelayedLoading } from '../../../shared/utils/delayedLoading';
import { api, modService } from '../../../shared/services/ipc';

/**
 * Internal refresh implementation
 * Refreshes only the currently selected category view for efficiency
 */
async function _refreshMods(profileId: string): Promise<void> {
  const { selectedCategory } = useModsStore.getState();

  // If a category is selected, refresh only that category's mods
  if (selectedCategory) {
    const { loadModsByCategory, loadUncategorizedMods } = await import('./categoryOperations');

    if (selectedCategory.id === 'UNCLASSIFIED') {
      await loadUncategorizedMods(profileId);
    } else {
      await loadModsByCategory(profileId, selectedCategory.id);
    }
  }
  // If no category selected, do nothing (user will see empty state)
}

/**
 * Refresh mods from backend (debounced 10ms to prevent mass IPC hits)
 * Only refreshes the currently selected category view, not all mods
 */
export const refreshMods = debounce(_refreshMods, 10);

/**
 * Update mod metadata
 * Uses delayed loading (100ms) to avoid flicker for fast updates
 */
export async function updateMod(
  profileId: string,
  sha: string,
  data: Partial<ModInfo>
): Promise<void> {
  const { setModsLoading, updateModLocal } = useModsStore.getState();
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

        // Update local state (Zustand automatically updates category filtered mods)
        updateModLocal(sha, data);

        notification.success('Mod updated successfully');
      },
      setModsLoading,
      100
    );
  } catch (error: unknown) {
    handleError(error);
    throw error;
  }
}

/**
 * Update mod locally without backend call (for optimistic updates)
 */
export function updateModLocal(sha: string, data: Partial<ModInfo>): void {
  useModsStore.getState().updateModLocal(sha, data);
}

/**
 * Delete a mod
 * Uses delayed loading (100ms) to avoid flicker for fast deletes
 */
export async function deleteMod(profileId: string, sha: string): Promise<void> {
  const { setModsLoading, removeMod } = useModsStore.getState();

  try {
    await executeWithDelayedLoading(
      async () => {
        await modService.deleteMod(profileId, sha);

        // Remove from local state
        removeMod(sha);

        notification.success('Mod deleted successfully');
      },
      setModsLoading,
      100
    );
  } catch (error: unknown) {
    handleError(error);
    throw error;
  }
}

export async function loadStatistics(profileId: string): Promise<void> {
  try {
    const statistics = await api.mod.getStatistics(profileId);
    useModsStore.getState().setStatistics(statistics);
  } catch (error: unknown) {
    console.error('Failed to load mod statistics:', error);
  }
}

/**
 * Load a mod in-game with optimistic updates
 */
export async function loadMod(profileId: string, sha: string): Promise<void> {
  const { optimisticLoadUpdate, optimisticUnloadUpdate } = useModsStore.getState();

  // 1. Apply optimistic update
  optimisticLoadUpdate(sha, []);

  try {
    // 2. Perform backend operation - returns affected mod SHAs
    const result = await modService.loadMod(profileId, sha);
    notification.success('Mod loaded successfully');

    // 3. Efficient partial update: Only update the loaded mod and unloaded mods
    if (result.unloadedModShas && result.unloadedModShas.length > 0) {
      // Update unloaded mods locally (Zustand automatically syncs all state)
      result.unloadedModShas.forEach((unloadedSha) => {
        optimisticUnloadUpdate(unloadedSha);
      });
    }
  } catch (error: unknown) {
    // 5. Revert optimistic update on error
    optimisticUnloadUpdate(sha);

    // Handle error with user-friendly messages
    handleError(error);
    throw error;
  }
}

/**
 * Unload a mod from game with optimistic updates
 */
export async function unloadMod(profileId: string, sha: string): Promise<void> {
  const { optimisticUnloadUpdate, optimisticLoadUpdate } = useModsStore.getState();

  // 1. Apply optimistic update
  optimisticUnloadUpdate(sha);

  try {
    // 2. Perform backend operation
    await modService.unloadMod(profileId, sha);
    notification.success('Mod unloaded successfully');
  } catch (error: unknown) {
    // 4. Revert optimistic update on error
    optimisticLoadUpdate(sha, []);

    // Handle error with user-friendly messages
    handleError(error);
    throw error;
  }
}


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
import { modService } from '../../../shared/services/ipc';

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
 * If category is updated, refreshes the category tree
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

        // If category changed, refresh category tree to update counts
        if (categoryChanged) {
          const { refreshCategoryTree } = await import('./categoryOperations');
          await refreshCategoryTree(profileId);
        }
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

        // Refresh to sync with backend
        await refreshMods(profileId);
      },
      setModsLoading,
      100
    );
  } catch (error: unknown) {
    handleError(error);
    throw error;
  }
}


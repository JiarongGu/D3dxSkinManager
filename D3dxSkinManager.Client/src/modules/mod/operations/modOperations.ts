/**
 * Mod operations - CRUD operations for mods
 * Centralized business logic with consistent error handling and state updates
 */

import { useModsStore } from '../store/modsStore';
import { modService } from '../services/modService';
import { ModInfo } from '../../../shared/types/mod.types';
import { notification } from '../../../shared/utils/notification';
import { handleError } from '../../../shared/utils/errorHandler';
import { executeWithDelayedLoading } from '../../../shared/utils/delayedLoading';

/**
 * Load all mods for a profile
 * Uses delayed loading (100ms) to avoid flicker for fast loads
 */
export async function loadMods(profileId: string): Promise<void> {
  const { setModsLoading, setError, setMods } = useModsStore.getState();

  setError(undefined);

  try {
    await executeWithDelayedLoading(
      async () => {
        const mods = await modService.getAllMods(profileId);
        setMods(mods);
      },
      setModsLoading,
      100
    );
  } catch (error) {
    const errorMessage = error instanceof Error ? error.message : 'Failed to load mods';
    setError(errorMessage);
    handleError(error);
  }
}

/**
 * Refresh mods from backend
 */
export async function refreshMods(profileId: string): Promise<void> {
  await loadMods(profileId);
}

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
        // Update metadata (name, author, tags, grading, description)
        await modService.updateMetadata(profileId, sha, {
          name: data.name,
          author: data.author,
          tags: data.tags,
          grading: data.grading,
          description: data.description,
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
  } catch (error) {
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
  } catch (error) {
    handleError(error);
    throw error;
  }
}

/**
 * Batch update mod metadata
 */
export async function batchUpdateMetadata(
  profileId: string,
  shas: string[],
  data: Partial<ModInfo>,
  fields: string[]
): Promise<void> {
  try {
    await modService.batchUpdateMetadata(profileId, shas, data, fields);

    // Update local state
    useModsStore.getState().updateModsLocal(shas, data);

    notification.success('Batch update successful');

    // Refresh to sync with backend
    await refreshMods(profileId);
  } catch (error) {
    handleError(error);
    throw error;
  }
}

/**
 * Export mods
 */
export async function exportMods(
  profileId: string,
  shas: string[],
  exportPath: string
): Promise<void> {
  try {
    // Export each mod individually
    for (const sha of shas) {
      await modService.exportMod(profileId, sha, exportPath);
    }
    notification.success(`Exported ${shas.length} mod(s) successfully`);
  } catch (error) {
    handleError(error);
    throw error;
  }
}

/**
 * Reset mod operations state
 */
export function resetModsState(): void {
  useModsStore.getState().reset();
}

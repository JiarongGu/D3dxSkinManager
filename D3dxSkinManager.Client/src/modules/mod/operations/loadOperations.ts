/**
 * Load/Unload operations - mod load state management with optimistic updates
 */

import { useModsStore } from '../store/modsStore';
import { modService } from '../services/modService';
import { notification } from '../../../shared/utils/notification';
import { handleError } from '../../../shared/utils/errorHandler';
import { refreshMods } from './modOperations';

/**
 * Load a mod in-game with optimistic updates
 */
export async function loadModInGame(profileId: string, sha: string): Promise<void> {
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

    // 4. Refresh mod info from backend to update hasCache and other properties
    await refreshMods(profileId);
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
export async function unloadModFromGame(profileId: string, sha: string): Promise<void> {
  const { optimisticUnloadUpdate, optimisticLoadUpdate } = useModsStore.getState();

  // 1. Apply optimistic update
  optimisticUnloadUpdate(sha);

  try {
    // 2. Perform backend operation
    await modService.unloadMod(profileId, sha);
    notification.success('Mod unloaded successfully');

    // 3. Refresh from backend to sync state
    await refreshMods(profileId);
  } catch (error: unknown) {
    // 4. Revert optimistic update on error
    optimisticLoadUpdate(sha, []);

    // Handle error with user-friendly messages
    handleError(error);
    throw error;
  }
}

/**
 * Unload all mods
 */
export async function unloadAllMods(profileId: string): Promise<void> {
  try {
    // Get all loaded mods
    const loadedShas = await modService.getLoadedMods(profileId);

    // Unload each mod
    await Promise.all(loadedShas.map((sha) => modService.unloadMod(profileId, sha)));

    notification.success('All mods unloaded successfully');

    // Refresh to sync with backend
    await refreshMods(profileId);
  } catch (error: unknown) {
    handleError(error);
    throw error;
  }
}

/**
 * Load multiple mods (no optimistic updates for batch operations)
 */
export async function loadMultipleMods(profileId: string, shas: string[]): Promise<void> {
  try {
    // Batch load doesn't support optimistic updates due to complexity
    // Just perform the operation and refresh
    await Promise.all(shas.map((sha) => modService.loadMod(profileId, sha)));

    notification.success(`Loaded ${shas.length} mod(s) successfully`);

    // Refresh to sync with backend
    await refreshMods(profileId);
  } catch (error: unknown) {
    handleError(error);
    throw error;
  }
}

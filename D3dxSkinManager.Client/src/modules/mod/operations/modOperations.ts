/**
 * Mod operations - CRUD operations for mods
 * Centralized business logic with consistent error handling and state updates
 */

import { useModsStore } from '../store/modsStore';
import { ModInfo } from '../../../shared/types/mod.types';
import { CATEGORY_IDS } from '../../../shared/types/category.types';
import { notification } from '../../../shared/utils/notification';
import { handleError } from '../../../shared/utils/errorHandler';
import { executeWithDelayedLoading } from '../../../shared/utils/delayedLoading';
import { api, modService, toolService } from '../../../shared/services/ipc';
import i18n from '../../../shared/services/i18n';
import logger from '../../../shared/utils/logger';

/**
 * Refresh mods from backend
 * Refreshes based on current view mode for efficiency
 * Note: Debouncing is handled by ModProvider (20ms) to prevent rapid-fire events
 */
export async function refreshMods(profileId: string): Promise<void> {
  const { selectedCategory, viewMode } = useModsStore.getState();

  // Import category operations
  const { loadModsByCategory, loadUnclassifiedMods, loadAllMods, loadLoadedMods } = await import('./categoryOperations');

  // Refresh based on current view mode
  switch (viewMode) {
    case 'all':
      await loadAllMods(profileId);
      break;

    case 'loaded':
      await loadLoadedMods(profileId);
      break;

    case 'unclassified':
      await loadUnclassifiedMods(profileId);
      break;

    case 'category':
    default:
      // If a category is selected, refresh that category's mods
      if (selectedCategory) {
        if (selectedCategory.id === CATEGORY_IDS.UNCLASSIFIED) {
          await loadUnclassifiedMods(profileId);
        } else {
          await loadModsByCategory(profileId, selectedCategory.id);
        }
      }
      break;
  }
}

/**
 * Set mod loading state (transient UI state)
 * Used when LOADING event is received to show loading indicator
 */
export function setModLoading(id: string, isLoading: boolean): void {
  const { updateModLocal } = useModsStore.getState();
  // Update only the isLoading flag locally (transient state)
  updateModLocal(id, { isLoading });
}

/**
 * Refresh a single mod in the mod list with fresh data from backend
 * Updates the mod's properties (isLoaded, hasCache, cachePath, etc.) without refreshing entire list
 * Used after load/unload events to verify cache state
 */
export async function refreshMod(profileId: string, id: string): Promise<void> {
  const { updateModLocal } = useModsStore.getState();

  try {
    // Fetch fresh enriched mod data from backend
    const freshMod = await modService.getModById(profileId, id);
    if (freshMod) {
      // Update the mod in the list with fresh data (and clear loading state)
      updateModLocal(id, { ...freshMod, isLoading: false });
    }
  } catch (error) {
    logger.error('Failed to refresh mod:', error);
  }
}

/**
 * Refresh the currently selected mod with fresh enriched data from backend
 * Called when specific mod events occur (LOADED, UNLOADED, METADATA_UPDATED, CACHE_CHANGED)
 * Ensures ModPreview shows up-to-date mod properties (hasCache, cachePath, isLoaded, etc.)
 */
export async function refreshSelectedMod(profileId: string): Promise<void> {
  const { selectedMod, setSelectedMod } = useModsStore.getState();

  if (!selectedMod?.id) {
    return; // No mod selected, nothing to refresh
  }

  try {
    // Fetch fresh enriched mod data from backend
    const freshMod = await modService.getModById(profileId, selectedMod.id);
    if (freshMod) {
      setSelectedMod(freshMod);
    } else {
      // Mod was deleted
      setSelectedMod(undefined);
    }
  } catch (error) {
    // Mod not found or error fetching, clear selection
    logger.error('Failed to refresh selected mod:', error);
    setSelectedMod(undefined);
  }
}

/**
 * Update mod metadata
 * Uses delayed loading (200ms) to avoid flicker for fast updates
 */
export async function updateMod(
  profileId: string,
  id: string,
  data: Partial<ModInfo>
): Promise<void> {
  const { updateModLocal, setModLoading } = useModsStore.getState();

  try {
    await executeWithDelayedLoading(
      async () => {
        // Update metadata (name, author, tags, grading, description, disablePreview)
        await modService.updateMetadata(profileId, id, {
          name: data.name,
          author: data.author,
          tags: data.tags,
          grading: data.grading,
          description: data.description,
          disablePreview: data.disablePreview,
        });

        // Update local state (Zustand automatically updates mods)
        updateModLocal(id, data);

        notification.success(i18n.t('mods.operations.updateSuccess'));
      },
      setModLoading,
      200
    );
  } catch (error: unknown) {
    handleError(error);
    throw error;
  }
}

/**
 * Delete a mod
 * Uses delayed loading (200ms) to avoid flicker for fast deletes
 */
export async function deleteMod(profileId: string, id: string): Promise<void> {
  const { removeMod, addBusyMod, removeBusyMod } = useModsStore.getState();

  addBusyMod(id);
  try {
    // Optimistically remove from list for instant feedback
    removeMod(id);

    await modService.deleteMod(profileId, id);
    notification.success(i18n.t('mods.operations.deleteSuccess'));
  } catch (error: unknown) {
    // Re-fetch on failure (mod wasn't actually deleted)
    await refreshMods(profileId);
    handleError(error);
  } finally {
    removeBusyMod(id);
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
    logger.error('Failed to load mod statistics:', error);
  }
}

/**
 * Load all currently loaded/active mods (any category) into the store. Drives the per-category
 * "active mod" indicator on the category cards / tree nodes.
 */
export async function refreshActiveMods(profileId: string): Promise<void> {
  try {
    const active = await modService.getActiveMods(profileId);
    useModsStore.getState().setActiveMods(active);
  } catch (error: unknown) {
    logger.error('Failed to load active mods:', error);
  }
}

/**
 * Load the latest per-mod health (warning/error only) from the most recent scan into the store.
 * Drives the mod-list "last scan" health badge. Point-in-time (not live) — refreshed after analysis.
 */
export async function refreshModHealth(profileId: string): Promise<void> {
  try {
    const summaries = await toolService.getLatestHealth(profileId);
    const map = Object.fromEntries(summaries.map((s) => [s.modId, s]));
    useModsStore.getState().setModHealth(map);
  } catch (error: unknown) {
    logger.error('Failed to load mod health:', error);
  }
}

/**
 * Load available tags for autocomplete
 */
export async function loadTags(profileId: string): Promise<void> {
  try {
    const tags = await modService.getTags(profileId);
    useModsStore.getState().setAvailableTags(tags);
  } catch (error: unknown) {
    logger.error('Failed to load tags:', error);
  }
}

/**
 * Load preview paths for the currently selected mod
 * Updates previewPaths in store and busts browser cache
 */
export async function loadPreviewPaths(profileId: string, id: string): Promise<void> {
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
        const paths = await api.mod.getPreviewPaths(profileId, id);
        setPreviewPaths(paths);
        bustPreviewCache(); // Bust browser cache
      },
      setPreviewLoading,
      200
    );
  } catch (error: unknown) {
    // Clear previews on error
    logger.error('Failed to load preview paths:', error);
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

  if (selectedMod?.id) {
    // Bust cache to force browser to reload images (prevents showing cached old images)
    bustPreviewCache();
    await loadPreviewPaths(profileId, selectedMod.id);
  }
}

/**
 * Load a mod in-game
 * Backend fires LOADED event -> ModProvider updates single mod optimistically + refreshes statistics
 */
export async function loadMod(profileId: string, id: string): Promise<void> {
  const { addBusyMod, removeBusyMod } = useModsStore.getState();

  addBusyMod(id);
  try {
    await modService.loadMod(profileId, id);
    notification.success(i18n.t('mods.operations.loadSuccess'));
  } catch (error: unknown) {
    handleError(error);
  } finally {
    removeBusyMod(id);
  }
}

/**
 * Unload a mod from game
 * Backend fires UNLOADED event -> ModProvider updates single mod optimistically + refreshes statistics
 */
export async function unloadMod(profileId: string, id: string): Promise<void> {
  const { addBusyMod, removeBusyMod } = useModsStore.getState();

  addBusyMod(id);
  try {
    await modService.unloadMod(profileId, id);
    notification.success(i18n.t('mods.operations.unloadSuccess'));
  } catch (error: unknown) {
    handleError(error);
  } finally {
    removeBusyMod(id);
  }
}


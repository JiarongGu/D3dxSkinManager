/**
 * Statistics Operations
 * Handles loading and updating mod statistics
 */

import { useModsStore } from '../store/modsStore';
import { api } from '../../../shared/services/ipc';
import { eventBus, Module, ModEventType } from '../../../shared/services/eventBus';

/**
 * Load mod statistics from backend
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
 * Refresh statistics (debounced to avoid spam)
 */
let refreshTimeout: NodeJS.Timeout | null = null;

export function refreshStatistics(profileId: string): void {
  if (refreshTimeout) {
    clearTimeout(refreshTimeout);
  }

  refreshTimeout = setTimeout(() => {
    void loadStatistics(profileId);
    refreshTimeout = null;
  }, 100); // 100ms debounce
}

/**
 * Subscribe to mod events to auto-refresh statistics
 * Call this once on app initialization
 */
export function subscribeToModStatisticsEvents(profileId: string): () => void {
  // Refresh statistics when mods are loaded/unloaded/deleted/imported
  const unsubscribeLoaded = eventBus.subscribe(
    Module.MOD,
    ModEventType.LOADED,
    () => refreshStatistics(profileId)
  );

  const unsubscribeUnloaded = eventBus.subscribe(
    Module.MOD,
    ModEventType.UNLOADED,
    () => refreshStatistics(profileId)
  );

  const unsubscribeDeleted = eventBus.subscribe(
    Module.MOD,
    ModEventType.DELETED,
    () => refreshStatistics(profileId)
  );

  const unsubscribeImported = eventBus.subscribe(
    Module.MOD,
    ModEventType.IMPORTED,
    () => refreshStatistics(profileId)
  );

  // Return cleanup function
  return () => {
    unsubscribeLoaded();
    unsubscribeUnloaded();
    unsubscribeDeleted();
    unsubscribeImported();
  };
}

/**
 * Thin ModsProvider - handles initialization and profile changes
 * No longer manages state (moved to Zustand store)
 */

import React, { useEffect, useCallback, useRef } from 'react';
import { useProfile } from '../../shared/context/ProfileContext';
import { useModsStore } from './store/modsStore';
import { eventBus, ModEventType, CategoryEventType, ProfileEventType, Module } from '../../shared/services/eventBus';
import { profileService } from '../../shared/services/ipc';
import * as modOps from './operations/modOperations';
import * as categoryOps from './operations/categoryOperations';
import { debounce } from 'lodash-es';
import { logger } from '../../shared/utils/logger';

interface ModsProviderProps {
  children: React.ReactNode;
}

/**
 * ModsProvider - manages mods module lifecycle
 *
 * Responsibilities:
 * - Initialize panel sizes from profile config for ModHierarchicalView
 * - REACTIVELY refresh mods when profile changes
 * - Subscribe to backend events
 * - Reset state when profile is cleared
 *
 * DESIGN PRINCIPLE:
 * The mods module listens to profile changes and refreshes itself automatically.
 * Other components (like ProfileSwitcher) don't need to manually trigger refresh.
 */
export const ModProvider: React.FC<ModsProviderProps> = ({ children }) => {
  const { selectedProfileId } = useProfile();
  const reset = useModsStore((state) => state.reset);
  const setPanelSizes = useModsStore((state) => state.setPanelSizes);

  // Track the current work path to detect changes
  const workPathRef = useRef<string | null>(null);

  // Load panel sizes from profile config
  const loadPanelSizes = useCallback(async () => {
    if (!selectedProfileId) {
      logger.info('[ModProvider] No profile selected, using default panel sizes');
      return;
    }

    try {
      const config = await profileService.getProfileConfiguration(selectedProfileId);
      const panelSize = config?.tabs?.mod?.panelSize;
      const lockedCategories = config?.tabs?.mod?.lockedExpandedCategories || [];

      if (panelSize) {
        logger.info('[ModProvider] Loading panel sizes from profile config:', panelSize);
        const [category, modList] = panelSize.split(' ').map(Number);
        if (!isNaN(category) && !isNaN(modList)) {
          const sizes = {
            categoryWidth: category,
            modListWidth: modList,
            previewWidth: 100 - category - modList
          };
          logger.info('[ModProvider] Setting panel sizes:', sizes);
          setPanelSizes(sizes);
        } else {
          logger.warn('[ModProvider] Invalid panel size format:', panelSize);
        }
      } else {
        logger.info('[ModProvider] No panel sizes found in profile config, using defaults');
      }

      // Load locked expanded categories
      logger.info('[ModProvider] Loading locked expanded categories:', lockedCategories);
      useModsStore.getState().setLockedCategories(lockedCategories);
    } catch (error) {
      logger.error('[ModProvider] Failed to load panel sizes:', error);
    }
  }, [selectedProfileId, setPanelSizes]);

  // Check if work path changed and refresh mods/categories if needed
  const checkWorkPathChange = useCallback(async () => {
    if (!selectedProfileId) return;

    const config = await profileService.getProfileConfiguration(selectedProfileId);
    const newWorkPath = config?.work?.mode === 'external'
      ? config?.work?.directory
      : config?.work?.internalWorkDirectory;

    const oldWorkPath = workPathRef.current;

    // Only refresh if work path actually changed
    if (oldWorkPath !== newWorkPath) {
      logger.info('[ModProvider] Work path changed from', oldWorkPath, 'to', newWorkPath, '- refreshing mods and categories');
      workPathRef.current = newWorkPath || null;
      void modOps.refreshMods(selectedProfileId);
      void categoryOps.refreshCategoryTree(selectedProfileId);
    } else {
      logger.info('[ModProvider] Config updated but work path unchanged - skipping refresh');
    }
  }, [selectedProfileId]);

  // Debounced handler for mod list updates (20ms prevents rapid-fire events)
  // Also refreshes statistics since they depend on mod list state
  const handleModListUpdate = useCallback(
    debounce(() => {
      if (!selectedProfileId) return;
      void modOps.refreshMods(selectedProfileId);
      void modOps.loadStatistics(selectedProfileId);
    }, 20),
    [selectedProfileId]
  );

  // Handler for specific mod events that affect the selected mod
  // Refreshes selectedMod with enriched data (hasCache, cachePath, isLoaded, etc.)
  // Debounced 20ms for deduplication when multiple events fire simultaneously
  const handleSelectedModUpdate = useCallback(
    debounce(() => {
      if (!selectedProfileId) return;
      void modOps.refreshSelectedMod(selectedProfileId);
    }, 20),
    [selectedProfileId]
  );

  // Debounced handler for category tree updates (20ms prevents bulk operation spam)
  // Also refreshes unclassified count since it depends on category assignments
  const handleCategoryTreeUpdate = useCallback(
    debounce(() => {
      if (!selectedProfileId) return;
      void categoryOps.refreshCategoryTree(selectedProfileId);
      void categoryOps.loadUnclassifiedCount(selectedProfileId);
    }, 20),
    [selectedProfileId]
  );

  // Load panel sizes on profile change
  useEffect(() => {
    void loadPanelSizes();
  }, [loadPanelSizes]);

  // Subscribe to backend events
  useEffect(() => {
    if (!selectedProfileId) return;

    const unsubscribeProfileConfigChanged = eventBus.subscribe(
      Module.PROFILE,
      ProfileEventType.CONFIG_UPDATED,
      () => {
        void loadPanelSizes();
        void checkWorkPathChange();
      }
    );

    const unsubscribeModListUpdated = eventBus.subscribe(
      Module.MOD,
      ModEventType.MOD_LIST_UPDATED,
      handleModListUpdate
    );

    const unsubscribeCategoryTreeUpdated = eventBus.subscribe(
      Module.CATEGORY,
      CategoryEventType.CATEGORY_TREE_UPDATED,
      handleCategoryTreeUpdate
    );

    const unsubscribePreviewImported = eventBus.subscribe(
      Module.MOD,
      ModEventType.PREVIEW_IMPORTED,
      () => void modOps.reloadCurrentPreview(selectedProfileId)
    );

    const unsubscribeThumbnailUpdated = eventBus.subscribe(
      Module.MOD,
      ModEventType.THUMBNAIL_UPDATED,
      () => void modOps.reloadCurrentPreview(selectedProfileId)
    );

    const unsubscribePreviewDeleted = eventBus.subscribe(
      Module.MOD,
      ModEventType.PREVIEW_DELETED,
      () => void modOps.reloadCurrentPreview(selectedProfileId)
    );

    // Subscribe to specific mod events that affect the selected mod
    // These events require refreshing selectedMod with enriched data
    const unsubscribeModLoaded = eventBus.subscribe(
      Module.MOD,
      ModEventType.LOADED,
      handleSelectedModUpdate
    );

    const unsubscribeModUnloaded = eventBus.subscribe(
      Module.MOD,
      ModEventType.UNLOADED,
      handleSelectedModUpdate
    );

    const unsubscribeMetadataUpdated = eventBus.subscribe(
      Module.MOD,
      ModEventType.METADATA_UPDATED,
      handleSelectedModUpdate
    );

    const unsubscribeCacheChanged = eventBus.subscribe(
      Module.MOD,
      ModEventType.CACHE_CHANGED,
      handleSelectedModUpdate
    );

    return () => {
      handleModListUpdate.cancel();
      handleCategoryTreeUpdate.cancel();
      handleSelectedModUpdate.cancel();

      unsubscribeProfileConfigChanged();
      unsubscribeModListUpdated();
      unsubscribeCategoryTreeUpdated();
      unsubscribePreviewImported();
      unsubscribeThumbnailUpdated();
      unsubscribePreviewDeleted();
      unsubscribeModLoaded();
      unsubscribeModUnloaded();
      unsubscribeMetadataUpdated();
      unsubscribeCacheChanged();
    };
  }, [selectedProfileId, loadPanelSizes, checkWorkPathChange, handleModListUpdate, handleCategoryTreeUpdate, handleSelectedModUpdate]);

  // Reload data on profile change
  useEffect(() => {
    if (selectedProfileId) {
      reset();
      void categoryOps.loadCategoryTree(selectedProfileId);
      void categoryOps.loadUnclassifiedCount(selectedProfileId);
      void modOps.loadStatistics(selectedProfileId);
      // Explicitly refresh mods to reload selected category (e.g., UNCLASSIFIED)
      // This ensures that if a category was selected before reset, its mods are refreshed
      void modOps.refreshMods(selectedProfileId);
    } else {
      reset();
    }
  }, [selectedProfileId, reset]);

  return <>{children}</>;
};

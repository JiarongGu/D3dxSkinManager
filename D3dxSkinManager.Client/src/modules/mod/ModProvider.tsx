/**
 * Thin ModsProvider - handles initialization and profile changes
 * No longer manages state (moved to Zustand store)
 */

import React, { useEffect, useCallback } from 'react';
import { useProfile } from '../../shared/context/ProfileContext';
import { useModsStore } from './store/modsStore';
import { eventBus, ModEventType, CategoryEventType, ProfileEventType, Module } from '../../shared/services/eventBus';
import { profileService } from '../../shared/services/ipc';
import * as modOps from './operations/modOperations';
import * as categoryOps from './operations/categoryOperations';
import { debounce } from 'lodash-es';

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

  // Load panel sizes from profile config
  const loadPanelSizes = useCallback(async () => {
    if (!selectedProfileId) {
      console.log('[ModProvider] No profile selected, using default panel sizes');
      return;
    }

    try {
      const config = await profileService.getProfileConfiguration(selectedProfileId);
      const panelSize = config?.tabs?.mod?.panelSize;
      const lockedCategories = config?.tabs?.mod?.lockedExpandedCategories || [];

      if (panelSize) {
        console.log('[ModProvider] Loading panel sizes from profile config:', panelSize);
        const [category, modList] = panelSize.split(' ').map(Number);
        if (!isNaN(category) && !isNaN(modList)) {
          const sizes = {
            categoryWidth: category,
            modListWidth: modList,
            previewWidth: 100 - category - modList
          };
          console.log('[ModProvider] Setting panel sizes:', sizes);
          setPanelSizes(sizes);
        } else {
          console.warn('[ModProvider] Invalid panel size format:', panelSize);
        }
      } else {
        console.log('[ModProvider] No panel sizes found in profile config, using defaults');
      }

      // Load locked expanded categories
      console.log('[ModProvider] Loading locked expanded categories:', lockedCategories);
      useModsStore.getState().setLockedCategories(lockedCategories);
    } catch (error) {
      console.error('[ModProvider] Failed to load panel sizes:', error);
    }
  }, [selectedProfileId, setPanelSizes]);

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

  // Debounced handler for category tree updates (20ms prevents bulk operation spam)
  const handleCategoryTreeUpdate = useCallback(
    debounce(() => {
      if (!selectedProfileId) return;
      categoryOps.refreshCategoryTree(selectedProfileId);
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
      () => void loadPanelSizes()
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

    return () => {
      handleModListUpdate.cancel();
      handleCategoryTreeUpdate.cancel();

      unsubscribeProfileConfigChanged();
      unsubscribeModListUpdated();
      unsubscribeCategoryTreeUpdated();
      unsubscribePreviewImported();
      unsubscribeThumbnailUpdated();
      unsubscribePreviewDeleted();
    };
  }, [selectedProfileId, loadPanelSizes, handleModListUpdate, handleCategoryTreeUpdate]);

  // Reload data on profile change
  useEffect(() => {
    if (selectedProfileId) {
      reset();
      void categoryOps.loadCategoryTree(selectedProfileId);
      void modOps.loadStatistics(selectedProfileId);
    } else {
      reset();
    }
  }, [selectedProfileId, reset]);

  return <>{children}</>;
};

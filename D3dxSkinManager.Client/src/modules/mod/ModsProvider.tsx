/**
 * Thin ModsProvider - handles initialization and profile changes
 * No longer manages state (moved to Zustand store)
 */

import React, { useEffect } from 'react';
import { useProfile } from '../../shared/context/ProfileContext';
import { useModsStore } from './store/modsStore';
import { eventBus, ModEventType, CategoryEventType, MigrationEventType, Module } from '../../shared/services/eventBus';
import { CATEGORY_IDS } from '../../shared/types/category.types';
import * as modOps from './operations/modOperations';
import * as categoryOps from './operations/categoryOperations';

interface ModsProviderProps {
  children: React.ReactNode;
}

/**
 * ModsProvider - manages mods module lifecycle
 *
 * Responsibilities:
 * - REACTIVELY refresh mods when profile changes
 * - Subscribe to backend events
 * - Reset state when profile is cleared
 *
 * DESIGN PRINCIPLE:
 * The mods module listens to profile changes and refreshes itself automatically.
 * Other components (like ProfileSwitcher) don't need to manually trigger refresh.
 */
export const ModsProvider: React.FC<ModsProviderProps> = ({ children }) => {
  const { selectedProfileId } = useProfile();
  const reset = useModsStore((state) => state.reset);

  // Subscribe to backend events
  useEffect(() => {
    if (!selectedProfileId) return;

    // Shared handler for mod state changes (load/unload)
    const handleModStateChange = () => {
      const state = useModsStore.getState();
      // If a category is selected, refresh the filtered mods from backend
      if (state.selectedCategory) {
        if (state.selectedCategory.id === CATEGORY_IDS.UNCLASSIFIED) {
          void categoryOps.loadUncategorizedMods(selectedProfileId);
        } else {
          void categoryOps.loadModsByCategory(selectedProfileId, state.selectedCategory.id);
        }
      }
      // Also refresh the main mod list to update status
      void modOps.refreshMods(selectedProfileId);
    };

    // Subscribe to backend mod events
    const unsubscribeModsRefreshed = eventBus.subscribe(Module.MOD, ModEventType.REFRESHED, () => {
      modOps.refreshMods(selectedProfileId);
    });

    // Subscribe to mod loaded/unloaded events to refresh both category-filtered mods AND category tree counts
    const unsubscribeModLoaded = eventBus.subscribe(Module.MOD, ModEventType.LOADED, () => {
      handleModStateChange();
      // Refresh category tree to update mod counts
      void categoryOps.refreshCategoryTree(selectedProfileId);
    });
    const unsubscribeModUnloaded = eventBus.subscribe(Module.MOD, ModEventType.UNLOADED, () => {
      handleModStateChange();
      // Refresh category tree to update mod counts
      void categoryOps.refreshCategoryTree(selectedProfileId);
    });

    // Subscribe to mod imported/deleted events to refresh category tree counts
    const unsubscribeModImported = eventBus.subscribe(Module.MOD, ModEventType.IMPORTED, () => {
      // Refresh category tree to update mod counts (new mod added)
      void categoryOps.refreshCategoryTree(selectedProfileId);
    });
    const unsubscribeModDeleted = eventBus.subscribe(Module.MOD, ModEventType.DELETED, () => {
      // Refresh category tree to update mod counts (mod removed)
      void categoryOps.refreshCategoryTree(selectedProfileId);
    });

    // Subscribe to mod metadata updated event to refresh mod list
    const unsubscribeModMetadataUpdated = eventBus.subscribe(Module.MOD, ModEventType.METADATA_UPDATED, () => {
      handleModStateChange();
    });

    // Subscribe to mod category updated event to refresh both mod list and category tree
    const unsubscribeModCategoryUpdated = eventBus.subscribe(Module.MOD, ModEventType.CATEGORY_UPDATED, () => {
      handleModStateChange();
      // Refresh category tree to update mod counts (mod moved to different category)
      void categoryOps.refreshCategoryTree(selectedProfileId);
    });

    const unsubscribeCategoryTreeUpdated = eventBus.subscribe(
      Module.CATEGORY,
      CategoryEventType.CATEGORY_TREE_UPDATED,
      () => {
        categoryOps.refreshCategoryTree(selectedProfileId);
      }
    );

    // Subscribe to migration completion events to reload profile data
    const unsubscribeMigrationCompleted = eventBus.subscribe(
      Module.MIGRATION,
      MigrationEventType.COMPLETED,
      () => {
        // Reload entire profile data after migration completes
        void Promise.all([
          modOps.loadMods(selectedProfileId),
          categoryOps.loadCategoryTree(selectedProfileId),
        ]);
      }
    );

    // Cleanup subscriptions on unmount or profile change
    return () => {
      unsubscribeModsRefreshed();
      unsubscribeModLoaded();
      unsubscribeModUnloaded();
      unsubscribeModImported();
      unsubscribeModDeleted();
      unsubscribeModMetadataUpdated();
      unsubscribeModCategoryUpdated();
      unsubscribeCategoryTreeUpdated();
      unsubscribeMigrationCompleted();
    };
  }, [selectedProfileId]);

  // REACTIVE: Handle profile changes automatically
  useEffect(() => {
    if (selectedProfileId) {
      // Load/refresh data for the new profile
      void Promise.all([
        modOps.loadMods(selectedProfileId),
        categoryOps.loadCategoryTree(selectedProfileId),
      ]);
    } else {
      // Reset state when no profile is selected
      reset();
    }
  }, [selectedProfileId, reset]);

  // Provider doesn't need to pass anything via context
  // Components can use useMods() hook directly
  return <>{children}</>;
};

/**
 * Main hook for accessing mods store
 * Provides access to all state and exposes operation functions
 */

import { useModsStore } from '../store/modsStore';
import { useProfile } from '../../../shared/context/ProfileContext';
import * as modOps from '../operations/modOperations';
import * as categoryOps from '../operations/categoryOperations';
import { ModInfo } from '../../../shared/types/mod.types';

/**
 * Main mods hook - provides state and operations
 */
export function useMods() {
  const { selectedProfileId } = useProfile();

  // Get state from store
  const state = useModsStore();

  // ============================================================
  // Wrapped operations with profileId injected
  // ============================================================

  const operations = {
    // Mod operations
    refreshMods: () => selectedProfileId && modOps.refreshMods(selectedProfileId),
    updateMod: (sha: string, data: Partial<ModInfo>) =>
      selectedProfileId && modOps.updateMod(selectedProfileId, sha, data),
    deleteMod: (id: string) => selectedProfileId && modOps.deleteMod(selectedProfileId, id),

    // Load operations
    loadMod: (id: string) =>
      selectedProfileId && modOps.loadMod(selectedProfileId, id),
    unloadMod: (id: string) =>
      selectedProfileId && modOps.unloadMod(selectedProfileId, id),

    // Category operations
    updateModCategory: (sha: string, categoryId: string) =>
      selectedProfileId && categoryOps.updateModCategory(selectedProfileId, sha, categoryId),
    updateModsCategory: (shas: string[], categoryId: string) =>
      selectedProfileId && categoryOps.batchUpdateCategories(selectedProfileId, shas, categoryId),

    // Category operations
    loadCategoryTree: () =>
      selectedProfileId && categoryOps.loadCategoryTree(selectedProfileId),
    refreshCategoryTree: () =>
      selectedProfileId && categoryOps.refreshCategoryTree(selectedProfileId),
    loadModsByCategory: (nodeId: string) =>
      selectedProfileId && categoryOps.loadModsByCategory(selectedProfileId, nodeId),
    loadUnclassifiedMods: () =>
      selectedProfileId && categoryOps.loadUnclassifiedMods(selectedProfileId),
    loadUnclassifiedCount: () =>
      selectedProfileId && categoryOps.loadUnclassifiedCount(selectedProfileId),
    loadAllMods: () =>
      selectedProfileId && categoryOps.loadAllMods(selectedProfileId),
    loadLoadedMods: () =>
      selectedProfileId && categoryOps.loadLoadedMods(selectedProfileId),
    clearCategoryFilter: categoryOps.clearCategoryFilter,
    selectCategory: (nodeId: string) =>
      selectedProfileId && categoryOps.selectCategory(selectedProfileId, nodeId),

    // Selection operations (direct store access)
    selectMod: state.setSelectedMod,
    selectMods: state.setSelectedMods,

    // UI operations (direct store access)
    openEditDialog: state.openEditDialog,
    closeEditDialog: state.closeEditDialog,
    setSearchQuery: state.setSearchQuery,
    setcategorySearch: state.setcategorySearch,
    setExpandedKeys: state.setExpandedKeys,
    setSelectedCategory: state.setSelectedCategory,
    setAvailableTags: state.setAvailableTags,

    // Import Workflow Screen operations
    openImportWorkflowScreen: state.openImportWorkflowScreen,
    closeImportWorkflowScreen: state.closeImportWorkflowScreen,

    // Batch Edit Screen operations
    openBatchEditScreen: state.openBatchEditScreen,
    closeBatchEditScreen: state.closeBatchEditScreen,

    // Global
    reset: state.reset,
  };

  return {
    // State
    state,

    // Operations
    ...operations,

    // Convenience getters
    selectedProfileId,
  };
}

/**
 * Hook to select specific state slices (for performance optimization)
 */
export function useModsState<T>(selector: (state: ReturnType<typeof useModsStore>) => T): T {
  return useModsStore(selector);
}

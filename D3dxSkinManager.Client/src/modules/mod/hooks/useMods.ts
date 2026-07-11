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
    updateMod: (id: string, data: Partial<ModInfo>) =>
      selectedProfileId && modOps.updateMod(selectedProfileId, id, data),
    deleteMod: (id: string) => selectedProfileId && modOps.deleteMod(selectedProfileId, id),

    // Load operations
    loadMod: (id: string) =>
      selectedProfileId && modOps.loadMod(selectedProfileId, id),
    unloadMod: (id: string) =>
      selectedProfileId && modOps.unloadMod(selectedProfileId, id),

    // Category operations
    updateModCategory: (id: string, categoryId: string) =>
      selectedProfileId && categoryOps.updateModCategory(selectedProfileId, id, categoryId),
    updateModsCategory: (ids: string[], categoryId: string) =>
      selectedProfileId && categoryOps.batchUpdateCategories(selectedProfileId, ids, categoryId),

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
    setCategorySearch: state.setCategorySearch,
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
 * Hook to select a slice of the mods store (perf: re-renders only when the slice changes).
 *
 * The selector's `state` type is DERIVED from the store via `ReturnType<typeof useModsStore.getState>`.
 * Do NOT write `ReturnType<typeof useModsStore>` — that is the HOOK's overloaded return type, which
 * resolves to `unknown`, silently making every `s.field` access an error. Guarded by useMods.test.tsx.
 */
export function useModsState<T>(selector: (state: ReturnType<typeof useModsStore.getState>) => T): T {
  return useModsStore(selector);
}

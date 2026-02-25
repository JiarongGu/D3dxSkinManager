/**
 * Main hook for accessing mods store
 * Provides access to all state and exposes operation functions
 */

import { useModsStore } from '../store/modsStore';
import { useProfile } from '../../../shared/context/ProfileContext';
import * as modOps from '../operations/modOperations';
import * as loadOps from '../operations/loadOperations';
import * as categoryOps from '../operations/categoryOperations';
import * as classificationOps from '../operations/classificationOperations';
import * as importOps from '../operations/importOperations';
import { ModInfo } from '../../../shared/types/mod.types';
import { ClassificationNode } from '../../../shared/types/classification.types';
import { ImportTask } from '../types/importTask.types';
import { ModManagementMode } from '../components/ModManagementScreen';

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
    loadMods: () => selectedProfileId && modOps.loadMods(selectedProfileId),
    refreshMods: () => selectedProfileId && modOps.refreshMods(selectedProfileId),
    updateMod: (sha: string, data: Partial<ModInfo>) =>
      selectedProfileId && modOps.updateMod(selectedProfileId, sha, data),
    updateModLocal: modOps.updateModLocal,
    deleteMod: (sha: string) => selectedProfileId && modOps.deleteMod(selectedProfileId, sha),
    batchUpdateMetadata: (shas: string[], data: Partial<ModInfo>, fields: string[]) =>
      selectedProfileId && modOps.batchUpdateMetadata(selectedProfileId, shas, data, fields),
    exportMods: (shas: string[], exportPath: string) =>
      selectedProfileId && modOps.exportMods(selectedProfileId, shas, exportPath),

    // Load operations
    loadModInGame: (sha: string) =>
      selectedProfileId && loadOps.loadModInGame(selectedProfileId, sha),
    unloadModFromGame: (sha: string) =>
      selectedProfileId && loadOps.unloadModFromGame(selectedProfileId, sha),
    unloadAllMods: () => selectedProfileId && loadOps.unloadAllMods(selectedProfileId),
    loadMultipleMods: (shas: string[]) =>
      selectedProfileId && loadOps.loadMultipleMods(selectedProfileId, shas),

    // Category operations
    updateModCategory: (sha: string, categoryId: string, onMismatch?: () => void) =>
      selectedProfileId &&
      categoryOps.updateModCategory(selectedProfileId, sha, categoryId, onMismatch),
    batchUpdateCategories: (shas: string[], categoryId: string) =>
      selectedProfileId && categoryOps.batchUpdateCategories(selectedProfileId, shas, categoryId),

    // Classification operations
    loadClassificationTree: () =>
      selectedProfileId && classificationOps.loadClassificationTree(selectedProfileId),
    refreshClassificationTree: () =>
      selectedProfileId && classificationOps.refreshClassificationTree(selectedProfileId),
    loadModsByClassification: (nodeId: string) =>
      selectedProfileId && classificationOps.loadModsByClassification(selectedProfileId, nodeId),
    loadUnclassifiedMods: () =>
      selectedProfileId && classificationOps.loadUnclassifiedMods(selectedProfileId),
    clearClassificationFilter: classificationOps.clearClassificationFilter,
    selectClassification: (nodeId: string) =>
      selectedProfileId && classificationOps.selectClassification(selectedProfileId, nodeId),

    // Import operations
    addImportTask: importOps.addImportTask,
    updateImportTask: importOps.updateImportTask,
    removeImportTask: importOps.removeImportTask,
    importMod: (task: ImportTask) =>
      selectedProfileId && importOps.importMod(selectedProfileId, task),
    importMods: (tasks: ImportTask[], onComplete?: () => void, onClose?: () => void) =>
      selectedProfileId && importOps.importMods(selectedProfileId, tasks, onComplete, onClose),
    clearImportTasks: importOps.clearImportTasks,
    updateMultipleTasks: importOps.updateMultipleTasks,

    // Selection operations (direct store access)
    selectMod: state.setSelectedMod,
    selectMods: state.setSelectedMods,
    setSelectedObject: state.setSelectedObject,

    // UI operations (direct store access)
    openEditDialog: state.openEditDialog,
    closeEditDialog: state.closeEditDialog,
    setSearchQuery: state.setSearchQuery,
    setClassificationSearch: state.setClassificationSearch,
    setExpandedKeys: state.setExpandedKeys,
    setSelectedClassification: state.setSelectedClassification,
    setAvailableTags: state.setAvailableTags,

    // ModManagementScreen operations
    openModManagementScreen: state.openModManagementScreen,
    closeModManagementScreen: state.closeModManagementScreen,
    batchUpdateImportTasks: importOps.updateMultipleTasks,
    addImportTasks: state.addImportTasks,
    setImportProcessing: state.setImportProcessing,

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

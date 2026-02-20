import React, {
  createContext,
  useContext,
  useCallback,
  useEffect,
  useMemo,
} from "react";
import { notification } from "../../../shared/utils/notification";
import { handleError } from "../../../shared/utils/errorHandler";
import { ModInfo } from "../../../shared/types/mod.types";
import { ClassificationNode } from "../../../shared/types/classification.types";
import { ImportTask } from "../components/AddModWindow";
import { useProfile } from "../../../shared/context/ProfileContext";
import { useModData } from "./hooks/useModData";
import { useClassificationData } from "./hooks/useClassificationData";
import { useImportOperations } from "./hooks/useImportOperations";
import { useModsUIState } from "./hooks/useModsUIState";
import { modService } from "../services/modService";

// ============================================================================
// Combined State Type (for backward compatibility)
// ============================================================================

interface ModsState {
  // Mod Data
  mods: ModInfo[];
  loading: boolean;
  error: string | null;
  selectedMod: ModInfo | null;
  selectedMods: ModInfo[];

  // Classification
  classificationTree: ClassificationNode[];
  classificationLoading: boolean;
  selectedClassification: ClassificationNode | null;
  classificationFilteredMods: ModInfo[] | null;
  classificationSearch: string;

  // UI State
  selectedObject: string;
  expandedKeys: React.Key[];
  searchQuery: string;
  editDialogVisible: boolean;
  tagDialogVisible: boolean;
  batchEditDialogVisible: boolean;
  importWindowVisible: boolean;
  addModUnitVisible: boolean;
  batchEditUnitVisible: boolean;
  modToEdit: ModInfo | null;
  currentTags: string[];
  currentEditTask: ImportTask | null;
  selectedTaskIds: string[];
  tagDialogContext: "mod" | "import";

  // Import State
  importTasks: ImportTask[];
  importProcessing: boolean;
  taskIdCounter: number;
}

// ============================================================================
// Context Interface
// ============================================================================

interface ModsContextValue {
  state: ModsState;
  actions: {
    // Data Actions
    loadMods: () => Promise<void>;
    refreshMods: () => Promise<void>;
    loadClassificationTree: () => Promise<void>;
    refreshClassificationTree: () => Promise<void>;
    loadModsByClassification: (nodeId: string) => Promise<void>;
    loadUnclassifiedMods: () => Promise<void>;

    // Selection Actions
    selectMod: (mod: ModInfo | null) => void;
    selectMods: (mods: ModInfo[]) => void;
    selectClassification: (node: ClassificationNode | null) => void;
    clearClassificationFilter: () => void;
    setSelectedObject: (object: string) => void;

    // Mod Operations
    importMod: (task: ImportTask) => Promise<ModInfo | null>;
    importMods: (tasks: ImportTask[]) => Promise<void>;
    updateMod: (sha: string, data: Partial<ModInfo>) => Promise<void>;
    deleteMod: (sha: string) => Promise<void>;
    loadModInGame: (sha: string) => Promise<void>;
    unloadModFromGame: (sha: string) => Promise<void>;
    updateModCategory: (
      sha: string,
      categoryId: string,
      onMismatch?: () => void,
    ) => Promise<boolean>;
    batchUpdateMetadata: (
      shas: string[],
      data: Partial<ModInfo>,
      fields: string[],
    ) => Promise<void>;

    // UI Actions
    setExpandedKeys: (keys: React.Key[]) => void;
    setSearchQuery: (query: string) => void;
    setClassificationSearch: (query: string) => void;

    // Import Task Actions
    addImportTasks: (tasks: ImportTask[]) => void;
    updateImportTask: (id: string, updates: Partial<ImportTask>) => void;
    removeImportTask: (id: string) => void;
    clearImportTasks: () => void;
    getNextTaskId: () => string;

    // Dialog Actions
    openEditDialog: (mod: ModInfo) => void;
    closeEditDialog: () => void;
    openTagDialog: (tags: string[], context: "mod" | "import") => void;
    closeTagDialog: () => void;
    saveTagsForMod: (tags: string[]) => void;
    saveTagsForImport: (tags: string[]) => void;
    openBatchEditDialog: (mods: ModInfo[]) => void;
    closeBatchEditDialog: () => void;
    openImportWindow: () => void;
    closeImportWindow: () => void;
    openAddModUnit: (task: ImportTask) => void;
    closeAddModUnit: () => void;
    saveAddModUnit: (task: ImportTask) => void;
    openBatchEditUnit: (taskIds: string[]) => void;
    closeBatchEditUnit: () => void;
    saveBatchEditUnit: (data: Partial<ModInfo>) => void;
  };
}

const ModsContext = createContext<ModsContextValue | undefined>(undefined);

// ============================================================================
// Provider
// ============================================================================

export const ModsProvider: React.FC<{
  children: React.ReactNode;
}> = ({ children }) => {
  const { selectedProfile, selectedProfileId } = useProfile();

  // Initialize all domain-specific hooks
  const modData = useModData();
  const classificationData = useClassificationData();
  const importOps = useImportOperations();
  const uiState = useModsUIState();

  // Load data when profile changes
  useEffect(() => {
    if (selectedProfile && selectedProfileId) {
      console.log(
        "[ModsContext] Profile selected/changed, loading mods for:",
        selectedProfile.name,
      );
      modData.loadMods(selectedProfileId);
      classificationData.loadClassificationTree(selectedProfileId);
    }
  }, [selectedProfileId]); // eslint-disable-line react-hooks/exhaustive-deps

  // Combine all state into a single object for backward compatibility
  const state: ModsState = useMemo(
    () => ({
      // Mod Data
      ...modData.state,

      // Classification
      ...classificationData.state,

      // UI State
      ...uiState.state,

      // Import State
      ...importOps.state,
    }),
    [modData.state, classificationData.state, uiState.state, importOps.state],
  );

  // ============================================================================
  // Action Wrappers
  // ============================================================================

  const loadMods = useCallback(async () => {
    if (!selectedProfileId) return;
    await modData.loadMods(selectedProfileId);
  }, [selectedProfileId, modData]);

  const refreshMods = useCallback(async () => {
    if (!selectedProfileId) return;
    await modData.refreshMods(selectedProfileId);
  }, [selectedProfileId, modData]);

  const loadClassificationTree = useCallback(async () => {
    if (!selectedProfileId) return;
    await classificationData.loadClassificationTree(selectedProfileId);
  }, [selectedProfileId, classificationData]);

  const refreshClassificationTree = useCallback(async () => {
    if (!selectedProfileId) return;
    await classificationData.refreshClassificationTree(selectedProfileId);
  }, [selectedProfileId, classificationData]);

  const loadModsByClassification = useCallback(
    async (nodeId: string) => {
      if (!selectedProfileId) return;
      await classificationData.loadModsByClassification(
        selectedProfileId,
        nodeId,
      );
    },
    [selectedProfileId, classificationData],
  );

  const loadUnclassifiedMods = useCallback(async () => {
    if (!selectedProfileId) return;
    await classificationData.loadUnclassifiedMods(selectedProfileId);
  }, [selectedProfileId, classificationData]);

  const importMod = useCallback(
    async (task: ImportTask) => {
      if (!selectedProfileId) return null;
      return await importOps.importMod(selectedProfileId, task);
    },
    [selectedProfileId, importOps],
  );

  const importMods = useCallback(
    async (tasks: ImportTask[]) => {
      if (!selectedProfileId) return;
      await importOps.importMods(
        selectedProfileId,
        tasks,
        () => refreshMods(),
        () => uiState.closeImportWindow(),
      );
    },
    [selectedProfileId, importOps, refreshMods, uiState],
  );

  const updateMod = useCallback(
    async (sha: string, data: Partial<ModInfo>) => {
      if (!selectedProfileId) return;
      await modData.updateMod(selectedProfileId, sha, data);
      // Also update the filtered mod if classification is active
      if (data.category !== undefined) {
        classificationData.dispatch({
          type: "UPDATE_FILTERED_MOD",
          payload: { sha, data, newCategory: data.category },
        });
      }
    },
    [selectedProfileId, modData, classificationData],
  );

  const deleteMod = useCallback(
    async (sha: string) => {
      if (!selectedProfileId) return;
      await modData.deleteMod(selectedProfileId, sha, () => refreshMods());
    },
    [selectedProfileId, modData, refreshMods],
  );

  const loadModInGame = useCallback(
    async (sha: string) => {
      if (!selectedProfileId) return;

      // Apply optimistic update to both mods and filtered mods
      modData.dispatch({
        type: "UPDATE_MOD_LOCAL",
        payload: { sha, data: { isLoaded: true } },
      });

      classificationData.dispatch({
        type: "UPDATE_FILTERED_MOD",
        payload: { sha, data: { isLoaded: true } },
      });

      try {
        // Perform backend operation - returns affected mod SHAs for efficient updates
        const result = await modService.loadMod(selectedProfileId, sha);
        notification.success("Mod loaded successfully");

        // Efficient partial update: Only update the loaded mod and unloaded mods
        // No need to refresh entire mod list (could be 1000s of mods)
        if (result.unloadedModShas && result.unloadedModShas.length > 0) {
          // Update unloaded mods locally
          result.unloadedModShas.forEach((unloadedSha) => {
            modData.dispatch({
              type: "UPDATE_MOD_LOCAL",
              payload: { sha: unloadedSha, data: { isLoaded: false } },
            });

            classificationData.dispatch({
              type: "UPDATE_FILTERED_MOD",
              payload: { sha: unloadedSha, data: { isLoaded: false } },
            });
          });
        }

        // Loaded mod is already updated optimistically above, no need to update again
      } catch (error) {
        // Revert optimistic update on error
        modData.dispatch({
          type: "UPDATE_MOD_LOCAL",
          payload: { sha, data: { isLoaded: false } },
        });

        classificationData.dispatch({
          type: "UPDATE_FILTERED_MOD",
          payload: { sha, data: { isLoaded: false } },
        });

        // Handle error with user-friendly messages based on error code
        handleError(error);
      }
    },
    [selectedProfileId, modData, classificationData],
  );

  const unloadModFromGame = useCallback(
    async (sha: string) => {
      if (!selectedProfileId) return;

      // Apply optimistic update to both mods and filtered mods
      modData.dispatch({
        type: "UPDATE_MOD_LOCAL",
        payload: { sha, data: { isLoaded: false } },
      });

      classificationData.dispatch({
        type: "UPDATE_FILTERED_MOD",
        payload: { sha, data: { isLoaded: false } },
      });

      try {
        // Perform backend operation
        await modService.unloadMod(selectedProfileId, sha);
        notification.success("Mod unloaded successfully");

        // Refresh mods from backend (with delayed loading to prevent flicker)
        await modData.loadMods(selectedProfileId);
      } catch (error) {
        // Revert optimistic update on error
        modData.dispatch({
          type: "UPDATE_MOD_LOCAL",
          payload: { sha, data: { isLoaded: true } },
        });

        classificationData.dispatch({
          type: "UPDATE_FILTERED_MOD",
          payload: { sha, data: { isLoaded: true } },
        });

        // Handle error with user-friendly messages based on error code
        handleError(error);
      }
    },
    [selectedProfileId, modData, classificationData],
  );

  const updateModCategory = useCallback(
    async (sha: string, categoryId: string, onMismatch?: () => void) => {
      if (!selectedProfileId) return false;

      // Capture current state before optimistic update
      const currentMods = modData.state.mods;
      const currentTree = classificationData.state.classificationTree;

      // Find the mod being updated
      const modBeingUpdated = currentMods.find((m) => m.sha === sha);
      const oldCategory = modBeingUpdated?.category;

      // If moving to the same category, do nothing
      if (oldCategory === categoryId) {
        return true;
      }

      // Calculate optimistic tree with updated counts
      const optimisticTree = updateTreeCounts(
        currentTree,
        currentMods,
        oldCategory,
        categoryId,
      );

      // Update mod data, classification filtered list, AND tree optimistically
      modData.dispatch({
        type: "UPDATE_MOD_LOCAL",
        payload: { sha, data: { category: categoryId } },
      });

      classificationData.dispatch({
        type: "UPDATE_FILTERED_MOD",
        payload: {
          sha,
          data: { category: categoryId },
          newCategory: categoryId,
        },
      });

      classificationData.dispatch({
        type: "SET_CLASSIFICATION_TREE",
        payload: optimisticTree,
      });

      try {
        // Perform backend operation
        await modService.updateCategory(selectedProfileId, sha, categoryId);
        notification.success("Category updated");

        // Refresh both mods and tree from backend (with delayed loading to prevent flicker)
        await Promise.all([
          modData.loadMods(selectedProfileId),
          classificationData.refreshClassificationTree(selectedProfileId),
        ]);

        return true;
      } catch (error) {
        // Revert optimistic update on error
        const originalMod = currentMods.find((m) => m.sha === sha);
        if (originalMod) {
          modData.dispatch({
            type: "UPDATE_MOD_LOCAL",
            payload: { sha, data: { category: originalMod.category } },
          });
        }
        notification.error("Failed to update category");

        // Refresh tree on error to ensure counts are correct
        if (onMismatch) {
          onMismatch();
        }
        return false;
      }
    },
    [selectedProfileId, modData, classificationData],
  );

  // Helper function to update tree counts when moving a mod between categories
  // Logic: -1 from old category, +1 to new category UNLESS new is ancestor of old
  const updateTreeCounts = (
    tree: ClassificationNode[],
    mods: ModInfo[],
    oldCategory: string | undefined,
    newCategory: string,
  ): ClassificationNode[] => {
    // Check if newCategory is an ancestor of oldCategory
    const isAncestor = (
      tree: ClassificationNode[],
      ancestorId: string,
      childId: string,
    ): boolean => {
      for (const node of tree) {
        if (node.id === ancestorId) {
          // Found the potential ancestor, check if childId exists in its subtree
          const hasChild = (n: ClassificationNode): boolean => {
            if (n.id === childId) return true;
            if (n.children) {
              return n.children.some(hasChild);
            }
            return false;
          };
          return hasChild(node);
        }
        if (node.children && isAncestor(node.children, ancestorId, childId)) {
          return true;
        }
      }
      return false;
    };

    const movingToAncestor = oldCategory
      ? isAncestor(tree, newCategory, oldCategory)
      : false;

    const updateNode = (node: ClassificationNode): ClassificationNode => {
      let updatedNode = { ...node };

      // Decrement old category
      if (oldCategory && node.id === oldCategory) {
        updatedNode.modCount = Math.max(0, (node.modCount || 0) - 1);
      }

      // Increment new category ONLY if not moving to ancestor
      // (if moving to ancestor, the count stays the same because child count already contributes to parent)
      if (node.id === newCategory && !movingToAncestor) {
        updatedNode.modCount = (node.modCount || 0) + 1;
      }

      // Recursively update children
      if (node.children && node.children.length > 0) {
        updatedNode.children = node.children.map(updateNode);
      }

      return updatedNode;
    };

    return tree.map(updateNode);
  };

  const batchUpdateMetadata = useCallback(
    async (shas: string[], data: Partial<ModInfo>, fields: string[]) => {
      if (!selectedProfileId) return;
      try {
        await modService.batchUpdateMetadata(
          selectedProfileId,
          shas,
          data,
          fields,
        );
        await refreshMods();
        notification.success("Batch update successful");
      } catch (error) {
        notification.error("Failed to batch update metadata");
        throw error;
      }
    },
    [selectedProfileId, refreshMods],
  );

  const clearClassificationFilter = useCallback(() => {
    classificationData.selectClassification(null);
    classificationData.dispatch({
      type: "SET_CLASSIFICATION_FILTERED_MODS",
      payload: null,
    });
  }, [classificationData]);

  // Tag dialog handlers
  const saveTagsForMod = useCallback(
    (tags: string[]) => {
      uiState.setCurrentTags(tags);
      uiState.closeTagDialog();

      if (uiState.state.modToEdit) {
        updateMod(uiState.state.modToEdit.sha, { tags });
      }
    },
    [uiState, updateMod],
  );

  const saveTagsForImport = useCallback(
    (tags: string[]) => {
      uiState.setCurrentTags(tags);
      uiState.closeTagDialog();

      if (uiState.state.currentEditTask) {
        importOps.updateImportTask(uiState.state.currentEditTask.id, {
          modData: { ...uiState.state.currentEditTask.modData, tags },
        });
      }
    },
    [uiState, importOps],
  );

  const saveAddModUnit = useCallback(
    (task: ImportTask) => {
      importOps.updateImportTask(task.id, task);
      uiState.closeAddModUnit();
    },
    [importOps, uiState],
  );

  const saveBatchEditUnit = useCallback(
    (data: Partial<ModInfo>) => {
      uiState.state.selectedTaskIds.forEach((taskId) => {
        const task = importOps.state.importTasks.find((t) => t.id === taskId);
        if (task) {
          importOps.updateImportTask(taskId, {
            modData: { ...task.modData, ...data },
          });
        }
      });
      uiState.closeBatchEditUnit();
    },
    [uiState, importOps],
  );

  // ============================================================================
  // Context Value
  // ============================================================================

  const value: ModsContextValue = useMemo(
    () => ({
      state,
      actions: {
        // Data
        loadMods,
        refreshMods,
        loadClassificationTree,
        refreshClassificationTree,
        loadModsByClassification,
        loadUnclassifiedMods,

        // Selection
        selectMod: modData.selectMod,
        selectMods: modData.selectMods,
        selectClassification: classificationData.selectClassification,
        clearClassificationFilter,
        setSelectedObject: uiState.setSelectedObject,

        // Mod Operations
        importMod,
        importMods,
        updateMod,
        deleteMod,
        loadModInGame,
        unloadModFromGame,
        updateModCategory,
        batchUpdateMetadata,

        // UI Actions
        setExpandedKeys: uiState.setExpandedKeys,
        setSearchQuery: uiState.setSearchQuery,
        setClassificationSearch: classificationData.setClassificationSearch,

        // Import Task Actions
        addImportTasks: importOps.addImportTasks,
        updateImportTask: importOps.updateImportTask,
        removeImportTask: importOps.removeImportTask,
        clearImportTasks: importOps.clearImportTasks,
        getNextTaskId: importOps.getNextTaskId,

        // Dialog Actions
        openEditDialog: uiState.openEditDialog,
        closeEditDialog: uiState.closeEditDialog,
        openTagDialog: uiState.openTagDialog,
        closeTagDialog: uiState.closeTagDialog,
        saveTagsForMod,
        saveTagsForImport,
        openBatchEditDialog: uiState.openBatchEditDialog,
        closeBatchEditDialog: uiState.closeBatchEditDialog,
        openImportWindow: uiState.openImportWindow,
        closeImportWindow: uiState.closeImportWindow,
        openAddModUnit: uiState.openAddModUnit,
        closeAddModUnit: uiState.closeAddModUnit,
        saveAddModUnit,
        openBatchEditUnit: uiState.openBatchEditUnit,
        closeBatchEditUnit: uiState.closeBatchEditUnit,
        saveBatchEditUnit,
      },
    }),
    [
      state,
      loadMods,
      refreshMods,
      loadClassificationTree,
      refreshClassificationTree,
      loadModsByClassification,
      loadUnclassifiedMods,
      modData,
      classificationData,
      clearClassificationFilter,
      uiState,
      importMod,
      importMods,
      updateMod,
      deleteMod,
      loadModInGame,
      unloadModFromGame,
      updateModCategory,
      batchUpdateMetadata,
      importOps,
      saveTagsForMod,
      saveTagsForImport,
      saveAddModUnit,
      saveBatchEditUnit,
    ],
  );

  return <ModsContext.Provider value={value}>{children}</ModsContext.Provider>;
};

// ============================================================================
// Hook
// ============================================================================

export function useMods() {
  const context = useContext(ModsContext);
  if (context === undefined) {
    throw new Error("useMods must be used within a ModsProvider");
  }
  return context;
}

// Backward compatibility export
export const useModsContext = useMods;

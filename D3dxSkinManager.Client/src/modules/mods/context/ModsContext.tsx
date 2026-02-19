import React, {
  createContext,
  useContext,
  useReducer,
  useCallback,
  useEffect,
} from "react";
import { message } from "antd";
import { ModInfo } from "../../../shared/types/mod.types";
import { ClassificationNode } from "../../../shared/types/classification.types";
import { modService } from "../services/modService";
import { classificationService } from "../../../shared/services/classificationService";
import {
  ImportTask,
  TaskStatus,
} from "../components/AddModWindow";
import { useProfile } from "../../../shared/context/ProfileContext";

// ============================================================================
// State Types
// ============================================================================

interface ModsState {
  // Data
  mods: ModInfo[];
  loading: boolean;
  error: string | null;

  // Classification
  classificationTree: ClassificationNode[];
  classificationLoading: boolean;
  selectedClassification: ClassificationNode | null;
  classificationFilteredMods: ModInfo[] | null;

  // Selection
  selectedMod: ModInfo | null;
  selectedMods: ModInfo[];
  selectedObject: string;

  // UI State
  expandedKeys: React.Key[];
  searchQuery: string;
  classificationSearch: string;

  // Import State
  importTasks: ImportTask[];
  importProcessing: boolean;
  taskIdCounter: number;

  // Dialog State
  editDialogVisible: boolean;
  tagDialogVisible: boolean;
  batchEditDialogVisible: boolean;
  importWindowVisible: boolean;
  addModUnitVisible: boolean;
  batchEditUnitVisible: boolean;

  // Edit State
  modToEdit: ModInfo | null;
  currentTags: string[];
  currentEditTask: ImportTask | null;
  selectedTaskIds: string[];
  tagDialogContext: "mod" | "import";
}

// ============================================================================
// Actions
// ============================================================================

type ModsAction =
  | { type: "SET_MODS"; payload: ModInfo[] }
  | { type: "SET_LOADING"; payload: boolean }
  | { type: "SET_ERROR"; payload: string | null }
  | { type: "SET_CLASSIFICATION_TREE"; payload: ClassificationNode[] }
  | { type: "SET_CLASSIFICATION_LOADING"; payload: boolean }
  | { type: "SELECT_CLASSIFICATION"; payload: ClassificationNode | null }
  | { type: "SET_CLASSIFICATION_FILTERED_MODS"; payload: ModInfo[] | null }
  | { type: "SELECT_MOD"; payload: ModInfo | null }
  | { type: "SELECT_MODS"; payload: ModInfo[] }
  | { type: "SET_SELECTED_OBJECT"; payload: string }
  | { type: "SET_EXPANDED_KEYS"; payload: React.Key[] }
  | { type: "SET_SEARCH_QUERY"; payload: string }
  | { type: "SET_CLASSIFICATION_SEARCH"; payload: string }
  | { type: "ADD_IMPORT_TASKS"; payload: ImportTask[] }
  | {
      type: "UPDATE_IMPORT_TASK";
      payload: { id: string; updates: Partial<ImportTask> };
    }
  | { type: "REMOVE_IMPORT_TASK"; payload: string }
  | { type: "CLEAR_IMPORT_TASKS" }
  | { type: "SET_IMPORT_PROCESSING"; payload: boolean }
  | { type: "INCREMENT_TASK_ID" }
  | { type: "OPEN_EDIT_DIALOG"; payload: ModInfo }
  | { type: "CLOSE_EDIT_DIALOG" }
  | {
      type: "OPEN_TAG_DIALOG";
      payload: { tags: string[]; context: "mod" | "import" };
    }
  | { type: "CLOSE_TAG_DIALOG" }
  | { type: "OPEN_BATCH_EDIT_DIALOG"; payload: ModInfo[] }
  | { type: "CLOSE_BATCH_EDIT_DIALOG" }
  | { type: "OPEN_IMPORT_WINDOW" }
  | { type: "CLOSE_IMPORT_WINDOW" }
  | { type: "OPEN_ADD_MOD_UNIT"; payload: ImportTask }
  | { type: "CLOSE_ADD_MOD_UNIT" }
  | { type: "OPEN_BATCH_EDIT_UNIT"; payload: string[] }
  | { type: "CLOSE_BATCH_EDIT_UNIT" }
  | { type: "SET_CURRENT_TAGS"; payload: string[] }
  | {
      type: "UPDATE_MOD_LOCAL";
      payload: { sha: string; data: Partial<ModInfo> };
    };

// ============================================================================
// Reducer
// ============================================================================

function modsReducer(state: ModsState, action: ModsAction): ModsState {
  switch (action.type) {
    case "SET_MODS":
      return { ...state, mods: action.payload, loading: false, error: null };

    case "SET_LOADING":
      return { ...state, loading: action.payload };

    case "SET_ERROR":
      return { ...state, error: action.payload, loading: false };

    case "SET_CLASSIFICATION_TREE":
      return {
        ...state,
        classificationTree: action.payload,
        classificationLoading: false,
      };

    case "SET_CLASSIFICATION_LOADING":
      return { ...state, classificationLoading: action.payload };

    case "SELECT_CLASSIFICATION":
      return { ...state, selectedClassification: action.payload };

    case "SET_CLASSIFICATION_FILTERED_MODS":
      return { ...state, classificationFilteredMods: action.payload };

    case "SELECT_MOD":
      return { ...state, selectedMod: action.payload };

    case "SELECT_MODS":
      return { ...state, selectedMods: action.payload };

    case "SET_SELECTED_OBJECT":
      return { ...state, selectedObject: action.payload };

    case "SET_EXPANDED_KEYS":
      return { ...state, expandedKeys: action.payload };

    case "SET_SEARCH_QUERY":
      return { ...state, searchQuery: action.payload };

    case "SET_CLASSIFICATION_SEARCH":
      return { ...state, classificationSearch: action.payload };

    case "ADD_IMPORT_TASKS":
      return {
        ...state,
        importTasks: [...state.importTasks, ...action.payload],
      };

    case "UPDATE_IMPORT_TASK":
      return {
        ...state,
        importTasks: state.importTasks.map((task) =>
          task.id === action.payload.id
            ? { ...task, ...action.payload.updates }
            : task,
        ),
      };

    case "REMOVE_IMPORT_TASK":
      return {
        ...state,
        importTasks: state.importTasks.filter(
          (task) => task.id !== action.payload,
        ),
      };

    case "CLEAR_IMPORT_TASKS":
      return { ...state, importTasks: [], taskIdCounter: 1 };

    case "SET_IMPORT_PROCESSING":
      return { ...state, importProcessing: action.payload };

    case "INCREMENT_TASK_ID":
      return { ...state, taskIdCounter: state.taskIdCounter + 1 };

    case "OPEN_EDIT_DIALOG":
      return { ...state, editDialogVisible: true, modToEdit: action.payload };

    case "CLOSE_EDIT_DIALOG":
      return { ...state, editDialogVisible: false, modToEdit: null };

    case "OPEN_TAG_DIALOG":
      return {
        ...state,
        tagDialogVisible: true,
        currentTags: action.payload.tags,
        tagDialogContext: action.payload.context,
      };

    case "CLOSE_TAG_DIALOG":
      return { ...state, tagDialogVisible: false };

    case "OPEN_BATCH_EDIT_DIALOG":
      return {
        ...state,
        batchEditDialogVisible: true,
        selectedMods: action.payload,
      };

    case "CLOSE_BATCH_EDIT_DIALOG":
      return { ...state, batchEditDialogVisible: false, selectedMods: [] };

    case "OPEN_IMPORT_WINDOW":
      return { ...state, importWindowVisible: true };

    case "CLOSE_IMPORT_WINDOW":
      return { ...state, importWindowVisible: false };

    case "OPEN_ADD_MOD_UNIT":
      return {
        ...state,
        addModUnitVisible: true,
        currentEditTask: action.payload,
      };

    case "CLOSE_ADD_MOD_UNIT":
      return { ...state, addModUnitVisible: false, currentEditTask: null };

    case "OPEN_BATCH_EDIT_UNIT":
      return {
        ...state,
        batchEditUnitVisible: true,
        selectedTaskIds: action.payload,
      };

    case "CLOSE_BATCH_EDIT_UNIT":
      return { ...state, batchEditUnitVisible: false, selectedTaskIds: [] };

    case "SET_CURRENT_TAGS":
      return { ...state, currentTags: action.payload };

    case "UPDATE_MOD_LOCAL":
      return {
        ...state,
        mods: state.mods.map((mod) =>
          mod.sha === action.payload.sha
            ? { ...mod, ...action.payload.data }
            : mod,
        ),
        selectedMod:
          state.selectedMod?.sha === action.payload.sha
            ? { ...state.selectedMod, ...action.payload.data }
            : state.selectedMod,
      };

    default:
      return state;
  }
}

// ============================================================================
// Context
// ============================================================================

interface ModsContextValue {
  state: ModsState;
  actions: {
    // Data Actions
    loadMods: () => Promise<void>;
    refreshMods: () => Promise<void>;
    loadClassificationTree: () => Promise<void>;
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

const initialState: ModsState = {
  mods: [],
  loading: false,
  error: null,
  classificationTree: [],
  classificationLoading: false,
  selectedClassification: null,
  classificationFilteredMods: null,
  selectedMod: null,
  selectedMods: [],
  selectedObject: "",
  expandedKeys: ["all"],
  searchQuery: "",
  classificationSearch: "",
  importTasks: [],
  importProcessing: false,
  taskIdCounter: 1,
  editDialogVisible: false,
  tagDialogVisible: false,
  batchEditDialogVisible: false,
  importWindowVisible: false,
  addModUnitVisible: false,
  batchEditUnitVisible: false,
  modToEdit: null,
  currentTags: [],
  currentEditTask: null,
  selectedTaskIds: [],
  tagDialogContext: "mod",
};

export const ModsProvider: React.FC<{
  children: React.ReactNode;
}> = ({ children }) => {
  const [state, dispatch] = useReducer(modsReducer, initialState);
  const { selectedProfile, selectedProfileId } = useProfile();

  // Load mods and classification tree when profile is selected or changes
  useEffect(() => {
    // Only load if we have a selected profile
    if (selectedProfile && selectedProfileId) {
      console.log(
        "[ModsContext] Profile selected/changed, loading mods for:",
        selectedProfile.name,
      );
      loadMods(selectedProfileId);
      loadClassificationTree(selectedProfileId);
    } else {
      // Clear mods if no profile is selected
      console.log("[ModsContext] No profile selected, clearing mods data");
      dispatch({ type: "SET_MODS", payload: [] });
      dispatch({ type: "SET_CLASSIFICATION_TREE", payload: [] });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedProfileId]); // React to profile ID changes

  // ============================================================================
  // Data Actions
  // ============================================================================

  const loadMods = useCallback(async (profileId?: string) => {
    const currentProfileId = profileId || selectedProfileId;
    if (!currentProfileId) {
      return;
    }
    dispatch({ type: "SET_LOADING", payload: true });
    try {
      const mods = await modService.getAllMods(currentProfileId);
      dispatch({ type: "SET_MODS", payload: mods });
    } catch (error) {
      const errorMessage =
        error instanceof Error ? error.message : "Failed to load mods";
      // Only show error if it's not the expected "Profile ID is required" error
      if (!errorMessage.includes("Profile ID is required")) {
        dispatch({ type: "SET_ERROR", payload: errorMessage });
        message.error(errorMessage);
      }
    }
  }, [selectedProfileId]);

  const refreshMods = useCallback(async () => {
    await loadMods();
  }, [loadMods]);

  const loadClassificationTree = useCallback(async (profileId?: string) => {
    const currentProfileId = profileId || selectedProfileId;
    if (!currentProfileId) {
      return;
    }
    dispatch({ type: "SET_CLASSIFICATION_LOADING", payload: true });
    try {
      const tree =
        await classificationService.getClassificationTree(currentProfileId);
      dispatch({ type: "SET_CLASSIFICATION_TREE", payload: tree });
    } catch (error) {
      const errorMessage =
        error instanceof Error
          ? error.message
          : "Failed to load classification tree";
      // Only log if it's not the expected "Profile ID is required" error
      if (!errorMessage.includes("Profile ID is required")) {
        console.error(
          "[ModsContext] Failed to load classification tree:",
          errorMessage,
        );
      }
      // Don't show error to user
      dispatch({ type: "SET_CLASSIFICATION_LOADING", payload: false });
    }
  }, [selectedProfileId]);

  const loadModsByClassification = useCallback(
    async (nodeId: string) => {
      if (!selectedProfileId) {
        return;
      }
      dispatch({ type: "SET_LOADING", payload: true });
      try {
        const mods = await modService.getModsByClassification(
          selectedProfileId,
          nodeId,
        );
        dispatch({ type: "SET_CLASSIFICATION_FILTERED_MODS", payload: mods });
        dispatch({ type: "SET_LOADING", payload: false });
      } catch (error) {
        const errorMessage =
          error instanceof Error
            ? error.message
            : "Failed to load mods by classification";
        console.error(
          "[ModsContext] Failed to load mods by classification:",
          errorMessage,
        );
        dispatch({ type: "SET_CLASSIFICATION_FILTERED_MODS", payload: [] });
        dispatch({ type: "SET_LOADING", payload: false });
      }
    },
    [selectedProfileId],
  );

  const loadUnclassifiedMods = useCallback(async () => {
    if (!selectedProfileId) {
      return;
    }
    dispatch({ type: "SET_LOADING", payload: true });
    try {
      const mods = await modService.getUnclassifiedMods(selectedProfileId);
      dispatch({ type: "SET_CLASSIFICATION_FILTERED_MODS", payload: mods });
      dispatch({ type: "SET_LOADING", payload: false });
    } catch (error) {
      const errorMessage =
        error instanceof Error
          ? error.message
          : "Failed to load unclassified mods";
      console.error(
        "[ModsContext] Failed to load unclassified mods:",
        errorMessage,
      );
      dispatch({ type: "SET_CLASSIFICATION_FILTERED_MODS", payload: [] });
      dispatch({ type: "SET_LOADING", payload: false });
    }
  }, [selectedProfileId]);

  // ============================================================================
  // Mod Operations
  // ============================================================================

  const importMod = useCallback(async (task: ImportTask) => {
    if (!selectedProfileId) {
      return null;
    }
    try {
      dispatch({
        type: "UPDATE_IMPORT_TASK",
        payload: {
          id: task.id,
          updates: { status: "processing" as TaskStatus, progress: 30 },
        },
      });

      const importedMod = await modService.importMod(selectedProfileId, task.filePath);

      dispatch({
        type: "UPDATE_IMPORT_TASK",
        payload: { id: task.id, updates: { progress: 60 } },
      });

      if (
        task.modData.name ||
        task.modData.author ||
        task.modData.tags ||
        task.modData.grading
      ) {
        await modService.updateMetadata(selectedProfileId, importedMod.sha, {
          name: task.modData.name,
          author: task.modData.author,
          tags: task.modData.tags,
          grading: task.modData.grading,
          description: task.modData.description,
        });
      }

      dispatch({
        type: "UPDATE_IMPORT_TASK",
        payload: {
          id: task.id,
          updates: {
            status: "success" as TaskStatus,
            progress: 100,
            message: "Import successful",
          },
        },
      });

      return importedMod;
    } catch (error) {
      const errorMessage =
        error instanceof Error ? error.message : "Import failed";
      dispatch({
        type: "UPDATE_IMPORT_TASK",
        payload: {
          id: task.id,
          updates: { status: "error" as TaskStatus, message: errorMessage },
        },
      });
      throw error;
    }
  }, [selectedProfileId]);

  const importMods = useCallback(
    async (tasks: ImportTask[]) => {
      dispatch({ type: "SET_IMPORT_PROCESSING", payload: true });
      let successCount = 0;
      let errorCount = 0;

      for (const task of tasks) {
        if (task.status !== "pending") continue;

        try {
          await importMod(task);
          successCount++;
        } catch (error) {
          errorCount++;
        }
      }

      dispatch({ type: "SET_IMPORT_PROCESSING", payload: false });

      if (errorCount === 0) {
        message.success(`Successfully imported ${successCount} mod(s)`);
      } else if (successCount > 0) {
        message.warning(
          `Imported ${successCount} mod(s), ${errorCount} failed`,
        );
      } else {
        message.error("Failed to import all mods");
      }

      if (successCount > 0) {
        await refreshMods();
      }

      if (errorCount === 0) {
        setTimeout(() => {
          dispatch({ type: "CLOSE_IMPORT_WINDOW" });
          dispatch({ type: "CLEAR_IMPORT_TASKS" });
        }, 2000);
      }
    },
    [importMod, refreshMods],
  );

  const updateMod = useCallback(
    async (sha: string, data: Partial<ModInfo>) => {
      if (!selectedProfileId) {
        return;
      }
      try {
        await modService.updateMetadata(selectedProfileId, sha, data);
        dispatch({ type: "UPDATE_MOD_LOCAL", payload: { sha, data } });
        message.success("Mod updated successfully");
      } catch (error) {
        message.error("Failed to update mod");
        throw error;
      }
    },
    [selectedProfileId],
  );

  const deleteMod = useCallback(
    async (sha: string) => {
      if (!selectedProfileId) {
        return;
      }
      try {
        await modService.deleteMod(selectedProfileId, sha);
        await refreshMods();
        message.success("Mod deleted successfully");
      } catch (error) {
        message.error("Failed to delete mod");
        throw error;
      }
    },
    [refreshMods, selectedProfileId],
  );

  const loadModInGame = useCallback(
    async (sha: string) => {
      if (!selectedProfileId) {
        return;
      }
      try {
        await modService.loadMod(selectedProfileId, sha);
        await refreshMods();
        message.success("Mod loaded successfully");
      } catch (error) {
        message.error("Failed to load mod");
        throw error;
      }
    },
    [refreshMods, selectedProfileId],
  );

  const unloadModFromGame = useCallback(
    async (sha: string) => {
      if (!selectedProfileId) {
        return;
      }
      try {
        await modService.unloadMod(selectedProfileId, sha);
        await refreshMods();
        message.success("Mod unloaded successfully");
      } catch (error) {
        message.error("Failed to unload mod");
        throw error;
      }
    },
    [refreshMods, selectedProfileId],
  );

  const batchUpdateMetadata = useCallback(
    async (
      shas: string[],
      data: Partial<ModInfo>,
      fields: string[],
    ) => {
      if (!selectedProfileId) {
        return;
      }
      try {
        await modService.batchUpdateMetadata(selectedProfileId, shas, data, fields);
        await refreshMods();
        message.success(`Updated ${shas.length} mod(s)`);
      } catch (error) {
        message.error("Failed to batch update mods");
        throw error;
      }
    },
    [refreshMods, selectedProfileId],
  );

  // ============================================================================
  // Dialog Actions
  // ============================================================================

  const saveTagsForMod = useCallback(
    (tags: string[]) => {
      dispatch({ type: "SET_CURRENT_TAGS", payload: tags });
      dispatch({ type: "CLOSE_TAG_DIALOG" });

      if (state.modToEdit) {
        updateMod(state.modToEdit.sha, { tags });
      }
    },
    [state.modToEdit, updateMod],
  );

  const saveTagsForImport = useCallback(
    (tags: string[]) => {
      dispatch({ type: "SET_CURRENT_TAGS", payload: tags });
      dispatch({ type: "CLOSE_TAG_DIALOG" });

      if (state.currentEditTask) {
        dispatch({
          type: "UPDATE_IMPORT_TASK",
          payload: {
            id: state.currentEditTask.id,
            updates: { modData: { ...state.currentEditTask.modData, tags } },
          },
        });
      }
    },
    [state.currentEditTask],
  );

  const saveAddModUnit = useCallback((task: ImportTask) => {
    dispatch({
      type: "UPDATE_IMPORT_TASK",
      payload: { id: task.id, updates: task },
    });
    dispatch({ type: "CLOSE_ADD_MOD_UNIT" });
  }, []);

  const saveBatchEditUnit = useCallback(
    (data: Partial<ModInfo>) => {
      state.selectedTaskIds.forEach((taskId) => {
        const task = state.importTasks.find((t) => t.id === taskId);
        if (task) {
          dispatch({
            type: "UPDATE_IMPORT_TASK",
            payload: {
              id: taskId,
              updates: { modData: { ...task.modData, ...data } },
            },
          });
        }
      });
      dispatch({ type: "CLOSE_BATCH_EDIT_UNIT" });
    },
    [state.selectedTaskIds, state.importTasks],
  );

  const getNextTaskId = useCallback(() => {
    const id = `TASK-${state.taskIdCounter}`;
    dispatch({ type: "INCREMENT_TASK_ID" });
    return id;
  }, [state.taskIdCounter]);

  // ============================================================================
  // Context Value
  // ============================================================================

  const value: ModsContextValue = {
    state,
    actions: {
      // Data
      loadMods,
      refreshMods,
      loadClassificationTree,
      loadModsByClassification,
      loadUnclassifiedMods,

      // Selection
      selectMod: (mod) => dispatch({ type: "SELECT_MOD", payload: mod }),
      selectMods: (mods) => dispatch({ type: "SELECT_MODS", payload: mods }),
      selectClassification: (node) =>
        dispatch({ type: "SELECT_CLASSIFICATION", payload: node }),
      clearClassificationFilter: () =>
        dispatch({ type: "SET_CLASSIFICATION_FILTERED_MODS", payload: null }),
      setSelectedObject: (object) =>
        dispatch({ type: "SET_SELECTED_OBJECT", payload: object }),

      // Mod Operations
      importMod,
      importMods,
      updateMod,
      deleteMod,
      loadModInGame,
      unloadModFromGame,
      batchUpdateMetadata,

      // UI
      setExpandedKeys: (keys) =>
        dispatch({ type: "SET_EXPANDED_KEYS", payload: keys }),
      setSearchQuery: (query) =>
        dispatch({ type: "SET_SEARCH_QUERY", payload: query }),
      setClassificationSearch: (query) =>
        dispatch({ type: "SET_CLASSIFICATION_SEARCH", payload: query }),

      // Import Tasks
      addImportTasks: (tasks) =>
        dispatch({ type: "ADD_IMPORT_TASKS", payload: tasks }),
      updateImportTask: (id, updates) =>
        dispatch({ type: "UPDATE_IMPORT_TASK", payload: { id, updates } }),
      removeImportTask: (id) =>
        dispatch({ type: "REMOVE_IMPORT_TASK", payload: id }),
      clearImportTasks: () => dispatch({ type: "CLEAR_IMPORT_TASKS" }),
      getNextTaskId,

      // Dialogs
      openEditDialog: (mod) =>
        dispatch({ type: "OPEN_EDIT_DIALOG", payload: mod }),
      closeEditDialog: () => dispatch({ type: "CLOSE_EDIT_DIALOG" }),
      openTagDialog: (tags, context) =>
        dispatch({ type: "OPEN_TAG_DIALOG", payload: { tags, context } }),
      closeTagDialog: () => dispatch({ type: "CLOSE_TAG_DIALOG" }),
      saveTagsForMod,
      saveTagsForImport,
      openBatchEditDialog: (mods) =>
        dispatch({ type: "OPEN_BATCH_EDIT_DIALOG", payload: mods }),
      closeBatchEditDialog: () => dispatch({ type: "CLOSE_BATCH_EDIT_DIALOG" }),
      openImportWindow: () => dispatch({ type: "OPEN_IMPORT_WINDOW" }),
      closeImportWindow: () => dispatch({ type: "CLOSE_IMPORT_WINDOW" }),
      openAddModUnit: (task) =>
        dispatch({ type: "OPEN_ADD_MOD_UNIT", payload: task }),
      closeAddModUnit: () => dispatch({ type: "CLOSE_ADD_MOD_UNIT" }),
      saveAddModUnit,
      openBatchEditUnit: (taskIds) =>
        dispatch({ type: "OPEN_BATCH_EDIT_UNIT", payload: taskIds }),
      closeBatchEditUnit: () => dispatch({ type: "CLOSE_BATCH_EDIT_UNIT" }),
      saveBatchEditUnit,
    },
  };

  return <ModsContext.Provider value={value}>{children}</ModsContext.Provider>;
};

// ============================================================================
// Hook
// ============================================================================

export const useModsContext = () => {
  const context = useContext(ModsContext);
  if (!context) {
    throw new Error("useModsContext must be used within ModsProvider");
  }
  return context;
};

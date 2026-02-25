/**
 * Centralized Mods Store using Zustand
 * Single source of truth for all mods module state
 *
 * Zustand provides:
 * - Built-in subscriptions (no need for separate event bus)
 * - Selector-based subscriptions (only re-render when specific state changes)
 * - Middleware support (immer for immutable updates)
 * - Simple API with React hooks
 */

import { create } from 'zustand';
import { immer } from 'zustand/middleware/immer';
import { ModInfo } from '../../../shared/types/mod.types';
import { ClassificationNode } from '../../../shared/types/classification.types';
import { ImportTask } from '../types/importTask.types';
import { ModManagementMode } from '../components/ModManagementScreen';

// ============================================================================
// State Interface
// ============================================================================

export interface ModsState {
  // Mod List Panel
  mods: ModInfo[];
  modsLoading: boolean; // Loading state for mod list panel
  error: string | undefined;
  selectedMod: ModInfo | undefined;
  selectedMods: ModInfo[];

  // Classification Panel
  classificationTree: ClassificationNode[];
  classificationLoading: boolean; // Loading state for classification tree panel
  selectedClassification: ClassificationNode | undefined;
  classificationFilteredMods: ModInfo[] | undefined;
  classificationSearch: string;

  // Preview Panel
  previewLoading: boolean; // Loading state for preview panel (images, metadata, etc.)

  // Import
  importTasks: ImportTask[];
  importProcessing: boolean;
  taskIdCounter: number;
  selectedTaskIds: string[];

  // UI State
  selectedObject: string;
  expandedKeys: React.Key[];
  searchQuery: string;
  editDialogVisible: boolean;
  modToEdit: ModInfo | undefined;
  availableTags: string[];

  // ModManagementScreen - Import queue state
  modManagementScreenVisible: boolean;
  modManagementMode: ModManagementMode; // Currently only 'import' mode
}

// ============================================================================
// Actions Interface
// ============================================================================

export interface ModsActions {
  // Mod List Panel Actions
  setMods: (mods: ModInfo[]) => void;
  setModsLoading: (loading: boolean) => void;
  setError: (error: string | undefined) => void;
  setSelectedMod: (mod: ModInfo | undefined) => void;
  setSelectedMods: (mods: ModInfo[]) => void;
  updateModLocal: (sha: string, data: Partial<ModInfo>) => void;
  updateModsLocal: (shas: string[], data: Partial<ModInfo>) => void;
  addMod: (mod: ModInfo) => void;
  removeMod: (sha: string) => void;
  optimisticLoadUpdate: (sha: string, unloadedShas: string[]) => void;
  optimisticUnloadUpdate: (sha: string) => void;
  optimisticCategoryUpdate: (sha: string, categoryId: string) => void;

  // Classification Panel Actions
  setClassificationTree: (tree: ClassificationNode[]) => void;
  setClassificationLoading: (loading: boolean) => void;
  setSelectedClassification: (node: ClassificationNode | undefined) => void;
  setClassificationFilteredMods: (mods: ModInfo[] | undefined) => void;

  // Preview Panel Actions
  setPreviewLoading: (loading: boolean) => void;
  setClassificationSearch: (search: string) => void;
  updateTreeNodeLocal: (nodeId: string, updates: Partial<ClassificationNode>) => void;
  clearClassificationFilter: () => void;

  // Import Actions
  setImportTasks: (tasks: ImportTask[]) => void;
  setImportProcessing: (processing: boolean) => void;
  setSelectedTaskIds: (ids: string[]) => void;
  addImportTask: (task: Omit<ImportTask, 'id'>) => string;
  addImportTasks: (tasks: ImportTask[]) => void;
  updateImportTask: (taskId: string, updates: Partial<ImportTask>) => void;
  removeImportTask: (taskId: string) => void;
  clearImportTasks: () => void;
  updateMultipleTasks: (taskIds: string[], updates: Partial<ImportTask>) => void;

  // UI Actions
  setSelectedObject: (object: string) => void;
  setExpandedKeys: (keys: React.Key[]) => void;
  toggleExpandedKey: (key: React.Key) => void;
  setSearchQuery: (query: string) => void;
  setAvailableTags: (tags: string[]) => void;
  openEditDialog: (mod: ModInfo) => void;
  closeEditDialog: () => void;

  // ModManagementScreen Actions
  openModManagementScreen: () => void;
  closeModManagementScreen: () => void;

  // Global Actions
  reset: () => void;
}

export type ModsStore = ModsState & ModsActions;

// ============================================================================
// Initial State
// ============================================================================

const initialState: ModsState = {
  // Mod List Panel
  mods: [],
  modsLoading: false,
  error: undefined,
  selectedMod: undefined,
  selectedMods: [],

  // Classification Panel
  classificationTree: [],
  classificationLoading: false,
  selectedClassification: undefined,
  classificationFilteredMods: undefined,
  classificationSearch: '',

  // Preview Panel
  previewLoading: false,

  // Import
  importTasks: [],
  importProcessing: false,
  taskIdCounter: 0,
  selectedTaskIds: [],

  // UI State
  selectedObject: '',
  expandedKeys: [],
  searchQuery: '',
  editDialogVisible: false,
  modToEdit: undefined,
  availableTags: [],

  // ModManagementScreen
  modManagementScreenVisible: false,
  modManagementMode: 'import',
};

// ============================================================================
// Store Creation with Zustand
// ============================================================================

export const useModsStore = create<ModsStore>()(
  immer((set, get) => ({
      ...initialState,

      // ============================================================
      // Mod Actions
      // ============================================================

      setMods: (mods) =>
        set((state) => {
          state.mods = mods;
        }),

      setModsLoading: (loading) =>
        set((state) => {
          state.modsLoading = loading;
        }),

      setError: (error) =>
        set((state) => {
          state.error = error;
        }),

      setSelectedMod: (mod) =>
        set((state) => {
          state.selectedMod = mod;
        }),

      setSelectedMods: (mods) =>
        set((state) => {
          state.selectedMods = mods;
        }),

      updateModLocal: (sha, data) =>
        set((state) => {
          state.mods = state.mods.map((mod: ModInfo) =>
            mod.sha === sha ? { ...mod, ...data } : mod
          );
          if (state.selectedMod?.sha === sha) {
            state.selectedMod = { ...state.selectedMod, ...data };
          }
          // Also update classification filtered mods if present
          if (state.classificationFilteredMods) {
            state.classificationFilteredMods = state.classificationFilteredMods.map((mod: ModInfo) =>
              mod.sha === sha ? { ...mod, ...data } : mod
            );
          }
        }),

      updateModsLocal: (shas, data) =>
        set((state) => {
          state.mods = state.mods.map((mod: ModInfo) =>
            shas.includes(mod.sha) ? { ...mod, ...data } : mod
          );
          if (state.selectedMod && shas.includes(state.selectedMod.sha)) {
            state.selectedMod = { ...state.selectedMod, ...data };
          }
          // Also update classification filtered mods if present
          if (state.classificationFilteredMods) {
            state.classificationFilteredMods = state.classificationFilteredMods.map((mod: ModInfo) =>
              shas.includes(mod.sha) ? { ...mod, ...data } : mod
            );
          }
        }),

      addMod: (mod) =>
        set((state) => {
          state.mods.push(mod);
        }),

      removeMod: (sha) =>
        set((state) => {
          state.mods = state.mods.filter((mod: ModInfo) => mod.sha !== sha);
          if (state.selectedMod?.sha === sha) {
            state.selectedMod = undefined;
          }
          state.selectedMods = state.selectedMods.filter((mod: ModInfo) => mod.sha !== sha);
          // Also update classification filtered mods if present
          if (state.classificationFilteredMods) {
            state.classificationFilteredMods = state.classificationFilteredMods.filter(
              (mod: ModInfo) => mod.sha !== sha
            );
          }
        }),

      optimisticLoadUpdate: (sha, unloadedShas) =>
        set((state) => {
          state.mods = state.mods.map((mod: ModInfo) => {
            if (mod.sha === sha) {
              return { ...mod, isLoaded: true };
            }
            if (unloadedShas.includes(mod.sha)) {
              return { ...mod, isLoaded: false };
            }
            return mod;
          });
          // Also update classification filtered mods if present
          if (state.classificationFilteredMods) {
            state.classificationFilteredMods = state.classificationFilteredMods.map((mod: ModInfo) => {
              if (mod.sha === sha) {
                return { ...mod, isLoaded: true };
              }
              if (unloadedShas.includes(mod.sha)) {
                return { ...mod, isLoaded: false };
              }
              return mod;
            });
          }
        }),

      optimisticUnloadUpdate: (sha) =>
        set((state) => {
          state.mods = state.mods.map((mod: ModInfo) =>
            mod.sha === sha ? { ...mod, isLoaded: false } : mod
          );
          // Also update classification filtered mods if present
          if (state.classificationFilteredMods) {
            state.classificationFilteredMods = state.classificationFilteredMods.map((mod: ModInfo) =>
              mod.sha === sha ? { ...mod, isLoaded: false } : mod
            );
          }
        }),

      optimisticCategoryUpdate: (sha, categoryId) =>
        set((state) => {
          state.mods = state.mods.map((mod: ModInfo) =>
            mod.sha === sha ? { ...mod, category: categoryId } : mod
          );
          // Also update classification filtered mods if present
          if (state.classificationFilteredMods) {
            state.classificationFilteredMods = state.classificationFilteredMods.map((mod: ModInfo) =>
              mod.sha === sha ? { ...mod, category: categoryId } : mod
            );
          }
        }),

      // ============================================================
      // Classification Actions
      // ============================================================

      setClassificationTree: (tree) =>
        set((state) => {
          state.classificationTree = tree;
        }),

      setClassificationLoading: (loading) =>
        set((state) => {
          state.classificationLoading = loading;
        }),

      setPreviewLoading: (loading) =>
        set((state) => {
          state.previewLoading = loading;
        }),

      setSelectedClassification: (node) =>
        set((state) => {
          state.selectedClassification = node;
        }),

      setClassificationFilteredMods: (mods) =>
        set((state) => {
          state.classificationFilteredMods = mods;
        }),

      setClassificationSearch: (search) =>
        set((state) => {
          state.classificationSearch = search;
        }),

      updateTreeNodeLocal: (nodeId, updates) =>
        set((state) => {
          const updateNode = (nodes: ClassificationNode[]): ClassificationNode[] => {
            return nodes.map((node) => {
              if (node.id === nodeId) {
                return { ...node, ...updates };
              }
              if (node.children && node.children.length > 0) {
                return { ...node, children: updateNode(node.children) };
              }
              return node;
            });
          };

          state.classificationTree = updateNode(state.classificationTree);
        }),

      clearClassificationFilter: () =>
        set((state) => {
          state.selectedClassification = undefined;
          state.classificationFilteredMods = undefined;
        }),

      // ============================================================
      // Import Actions
      // ============================================================

      setImportTasks: (tasks) =>
        set((state) => {
          state.importTasks = tasks;
        }),

      setImportProcessing: (processing) =>
        set((state) => {
          state.importProcessing = processing;
        }),

      setSelectedTaskIds: (ids) =>
        set((state) => {
          state.selectedTaskIds = ids;
        }),

      addImportTask: (task) => {
        const taskId = `TASK-${get().taskIdCounter + 1}`;
        const newTask: ImportTask = { ...task, id: taskId };

        set((state) => {
          state.importTasks.push(newTask);
          state.taskIdCounter++;
        });

        return taskId;
      },

      addImportTasks: (tasks) =>
        set((state) => {
          state.importTasks.push(...tasks);
          state.taskIdCounter += tasks.length;
        }),

      updateImportTask: (taskId, updates) =>
        set((state) => {
          state.importTasks = state.importTasks.map((task: ImportTask) =>
            task.id === taskId ? { ...task, ...updates } : task
          );
        }),

      removeImportTask: (taskId) =>
        set((state) => {
          state.importTasks = state.importTasks.filter((task: ImportTask) => task.id !== taskId);
          state.selectedTaskIds = state.selectedTaskIds.filter((id: string) => id !== taskId);
        }),

      clearImportTasks: () =>
        set((state) => {
          state.importTasks = [];
          state.selectedTaskIds = [];
        }),

      updateMultipleTasks: (taskIds, updates) =>
        set((state) => {
          state.importTasks = state.importTasks.map((task: ImportTask) =>
            taskIds.includes(task.id) ? { ...task, ...updates } : task
          );
        }),

      // ============================================================
      // UI Actions
      // ============================================================

      setSelectedObject: (object) =>
        set((state) => {
          state.selectedObject = object;
        }),

      setExpandedKeys: (keys) =>
        set((state) => {
          state.expandedKeys = keys;
        }),

      toggleExpandedKey: (key) =>
        set((state) => {
          const isExpanded = state.expandedKeys.includes(key);
          state.expandedKeys = isExpanded
            ? state.expandedKeys.filter((k: React.Key) => k !== key)
            : [...state.expandedKeys, key];
        }),

      setSearchQuery: (query) =>
        set((state) => {
          state.searchQuery = query;
        }),

      setAvailableTags: (tags) =>
        set((state) => {
          state.availableTags = tags;
        }),

      openEditDialog: (mod) =>
        set((state) => {
          state.editDialogVisible = true;
          state.modToEdit = mod;
        }),

      closeEditDialog: () =>
        set((state) => {
          state.editDialogVisible = false;
          state.modToEdit = undefined;
        }),

      // ============================================================
      // ModManagementScreen Actions
      // ============================================================

      openModManagementScreen: () =>
        set((state) => {
          state.modManagementScreenVisible = true;
          state.modManagementMode = 'import';
        }),

      closeModManagementScreen: () =>
        set((state) => {
          state.modManagementScreenVisible = false;
          // Reset selection when closing
          state.selectedTaskIds = [];
        }),

      // ============================================================
      // Global Actions
      // ============================================================

      reset: () => set(initialState),
    }))
);

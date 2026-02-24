/**
 * UI slice - manages UI-only state (dialogs, expanded keys, etc.)
 */

import { ModInfo } from '../../../../shared/types/mod.types';
import { ImportTask } from '../../components/AddModWindow';

export interface UISliceState {
  // Object filter
  selectedObject: string;

  // Tree expansion
  expandedKeys: React.Key[];

  // Search
  searchQuery: string;

  // Dialogs
  editDialogVisible: boolean;
  tagDialogVisible: boolean;
  batchEditDialogVisible: boolean;
  importWindowVisible: boolean;
  addModUnitVisible: boolean;
  batchEditUnitVisible: boolean;

  // Dialog context
  modToEdit: ModInfo | undefined;
  currentTags: string[];
  currentEditTask: ImportTask | undefined;
  tagDialogContext: 'mod' | 'import';
}

export const initialUIState: UISliceState = {
  selectedObject: '',
  expandedKeys: [],
  searchQuery: '',
  editDialogVisible: false,
  tagDialogVisible: false,
  batchEditDialogVisible: false,
  importWindowVisible: false,
  addModUnitVisible: false,
  batchEditUnitVisible: false,
  modToEdit: undefined,
  currentTags: [],
  currentEditTask: undefined,
  tagDialogContext: 'mod',
};

export interface UISliceActions {
  // Object filter
  setSelectedObject: (object: string) => void;

  // Tree expansion
  setExpandedKeys: (keys: React.Key[]) => void;
  toggleExpandedKey: (key: React.Key) => void;

  // Search
  setSearchQuery: (query: string) => void;

  // Dialog management
  openEditDialog: (mod: ModInfo) => void;
  closeEditDialog: () => void;

  openTagDialog: (context: 'mod' | 'import', initialTags: string[], task?: ImportTask) => void;
  closeTagDialog: () => void;

  openBatchEditDialog: (mods: ModInfo[]) => void;
  closeBatchEditDialog: () => void;

  openImportWindow: () => void;
  closeImportWindow: () => void;

  openAddModUnit: (task: ImportTask) => void;
  closeAddModUnit: () => void;

  openBatchEditUnit: (taskIds: string[]) => void;
  closeBatchEditUnit: () => void;

  // Reset
  reset: () => void;
}

export const createUISliceActions = (
  set: (fn: (state: UISliceState) => Partial<UISliceState>) => void,
  get: () => UISliceState
): UISliceActions => ({
  setSelectedObject: (object) => set(() => ({ selectedObject: object })),

  setExpandedKeys: (keys) => set(() => ({ expandedKeys: keys })),

  toggleExpandedKey: (key) =>
    set((state) => {
      const isExpanded = state.expandedKeys.includes(key);
      return {
        expandedKeys: isExpanded
          ? state.expandedKeys.filter((k) => k !== key)
          : [...state.expandedKeys, key],
      };
    }),

  setSearchQuery: (query) => set(() => ({ searchQuery: query })),

  openEditDialog: (mod) =>
    set(() => ({
      editDialogVisible: true,
      modToEdit: mod,
    })),

  closeEditDialog: () =>
    set(() => ({
      editDialogVisible: false,
      modToEdit: undefined,
    })),

  openTagDialog: (context, initialTags, task) =>
    set(() => ({
      tagDialogVisible: true,
      tagDialogContext: context,
      currentTags: initialTags,
      currentEditTask: task,
    })),

  closeTagDialog: () =>
    set(() => ({
      tagDialogVisible: false,
      currentTags: [],
      currentEditTask: undefined,
    })),

  openBatchEditDialog: (mods) => set(() => ({ batchEditDialogVisible: true })),

  closeBatchEditDialog: () => set(() => ({ batchEditDialogVisible: false })),

  openImportWindow: () => set(() => ({ importWindowVisible: true })),

  closeImportWindow: () => set(() => ({ importWindowVisible: false })),

  openAddModUnit: (task) => set(() => ({
    addModUnitVisible: true,
    currentEditTask: task
  })),

  closeAddModUnit: () => set(() => ({
    addModUnitVisible: false,
    currentEditTask: undefined
  })),

  openBatchEditUnit: (taskIds) => set(() => ({ batchEditUnitVisible: true })),

  closeBatchEditUnit: () => set(() => ({ batchEditUnitVisible: false })),

  reset: () => set(() => initialUIState),
});

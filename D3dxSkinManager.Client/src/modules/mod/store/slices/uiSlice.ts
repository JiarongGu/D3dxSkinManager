/**
 * UI slice - manages UI-only state (dialogs, expanded keys, etc.)
 */

import { ModInfo } from '../../../../shared/types/mod.types';

export interface UISliceState {
  // Tree expansion
  expandedKeys: React.Key[];

  // Search
  searchQuery: string;

  // Dialogs
  editDialogVisible: boolean;
  tagDialogVisible: boolean;
  batchEditDialogVisible: boolean;

  // Dialog context
  modToEdit: ModInfo | undefined;
  currentTags: string[];
  tagDialogContext: 'mod' | 'import';
}

export const initialUIState: UISliceState = {
  expandedKeys: [],
  searchQuery: '',
  editDialogVisible: false,
  tagDialogVisible: false,
  batchEditDialogVisible: false,
  modToEdit: undefined,
  currentTags: [],
  tagDialogContext: 'mod',
};

export interface UISliceActions {
  // Tree expansion
  setExpandedKeys: (keys: React.Key[]) => void;
  toggleExpandedKey: (key: React.Key) => void;

  // Search
  setSearchQuery: (query: string) => void;

  // Dialog management
  openEditDialog: (mod: ModInfo) => void;
  closeEditDialog: () => void;

  openTagDialog: (context: 'mod' | 'import', initialTags: string[]) => void;
  closeTagDialog: () => void;

  openBatchEditDialog: (mods: ModInfo[]) => void;
  closeBatchEditDialog: () => void;

  // Reset
  reset: () => void;
}

export const createUISliceActions = (
  set: (fn: (state: UISliceState) => Partial<UISliceState>) => void,
  get: () => UISliceState
): UISliceActions => ({
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

  openTagDialog: (context, initialTags) =>
    set(() => ({
      tagDialogVisible: true,
      tagDialogContext: context,
      currentTags: initialTags,
    })),

  closeTagDialog: () =>
    set(() => ({
      tagDialogVisible: false,
      currentTags: [],
    })),

  openBatchEditDialog: (mods) => set(() => ({ batchEditDialogVisible: true })),

  closeBatchEditDialog: () => set(() => ({ batchEditDialogVisible: false })),

  reset: () => set(() => initialUIState),
});

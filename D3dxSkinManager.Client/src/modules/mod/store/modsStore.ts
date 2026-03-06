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
import { ModInfo, ModStatistics } from '../../../shared/types/mod.types';
import { CategoryInfo } from '../../../shared/types/category.types';

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

  // Statistics (global mod stats - not affected by category selection)
  statistics: ModStatistics | undefined;

  // Category Panel
  CategoryTree: CategoryInfo[];
  CategoryLoading: boolean; // Loading state for Category tree panel
  selectedCategory: CategoryInfo | undefined;
  CategoryFilteredMods: ModInfo[] | undefined;
  categorySearch: string;

  // Preview Panel
  previewLoading: boolean; // Loading state for preview panel (images, metadata, etc.)
  previewPaths: string[]; // Image preview paths for selected mod
  previewCacheTimestamp: number; // Cache buster for browser image cache

  // UI State
  expandedKeys: React.Key[];
  lockedCategories: string[]; // Persisted locked category IDs
  searchQuery: string;
  editDialogVisible: boolean;
  modToEdit: ModInfo | undefined;
  availableTags: string[];

  // Panel Sizes (for resizable panels)
  panelSizes: {
    categoryWidth: number; // percentage
    modListWidth: number; // percentage
    previewWidth: number; // percentage (calculated)
  };

  // Import Workflow Screen
  importWorkflowScreenVisible: boolean;
}

// ============================================================================
// Actions Interface
// ============================================================================

export interface ModsActions {
  // Mod List Panel Actions
  setModsLoading: (loading: boolean) => void;
  setSelectedMod: (mod: ModInfo | undefined) => void;
  setSelectedMods: (mods: ModInfo[]) => void;
  updateModLocal: (sha: string, data: Partial<ModInfo>) => void;
  removeMod: (sha: string) => void;
  optimisticLoadUpdate: (sha: string, unloadedShas: string[]) => void;
  optimisticUnloadUpdate: (sha: string) => void;
  optimisticCategoryUpdate: (sha: string, categoryId: string) => void;

  // Statistics Actions
  setStatistics: (statistics: ModStatistics) => void;

  // Category Panel Actions
  setCategoryTree: (tree: CategoryInfo[]) => void;
  setCategoryLoading: (loading: boolean) => void;
  setSelectedCategory: (node: CategoryInfo | undefined) => void;
  setCategoryFilteredMods: (mods: ModInfo[] | undefined) => void;

  // Preview Panel Actions
  setPreviewLoading: (loading: boolean) => void;
  setPreviewPaths: (paths: string[]) => void;
  bustPreviewCache: () => void;
  setcategorySearch: (search: string) => void;
  clearCategoryFilter: () => void;

  // UI Actions
  setExpandedKeys: (keys: React.Key[]) => void;
  setLockedCategories: (keys: string[]) => void;
  addLockedCategory: (key: string) => void;
  removeLockedCategory: (key: string) => void;
  setSearchQuery: (query: string) => void;
  setAvailableTags: (tags: string[]) => void;
  openEditDialog: (mod: ModInfo) => void;
  closeEditDialog: () => void;

  // Panel Size Actions
  setPanelSizes: (sizes: { categoryWidth: number; modListWidth: number; previewWidth: number }) => void;

  // Import Workflow Screen Actions
  openImportWorkflowScreen: () => void;
  closeImportWorkflowScreen: () => void;

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

  // Statistics
  statistics: undefined,

  // Category Panel
  CategoryTree: [],
  CategoryLoading: false,
  selectedCategory: undefined,
  CategoryFilteredMods: undefined,
  categorySearch: '',

  // Preview Panel
  previewLoading: false,
  previewPaths: [],
  previewCacheTimestamp: Date.now(),

  // UI State
  expandedKeys: [],
  lockedCategories: [],
  searchQuery: '',
  editDialogVisible: false,
  modToEdit: undefined,
  availableTags: [],

  // Panel Sizes
  panelSizes: {
    categoryWidth: 20,
    modListWidth: 35,
    previewWidth: 45,
  },

  // Import Workflow Screen
  importWorkflowScreenVisible: false,
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

      setModsLoading: (loading) =>
        set((state) => {
          state.modsLoading = loading;
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
          // Also update Category filtered mods if present
          if (state.CategoryFilteredMods) {
            state.CategoryFilteredMods = state.CategoryFilteredMods.map((mod: ModInfo) =>
              mod.sha === sha ? { ...mod, ...data } : mod
            );
          }
        }),

      removeMod: (sha) =>
        set((state) => {
          state.mods = state.mods.filter((mod: ModInfo) => mod.sha !== sha);
          if (state.selectedMod?.sha === sha) {
            state.selectedMod = undefined;
          }
          state.selectedMods = state.selectedMods.filter((mod: ModInfo) => mod.sha !== sha);
          // Also update Category filtered mods if present
          if (state.CategoryFilteredMods) {
            state.CategoryFilteredMods = state.CategoryFilteredMods.filter(
              (mod: ModInfo) => mod.sha !== sha
            );
          }
        }),

      optimisticLoadUpdate: (sha, unloadedShas) =>
        set((state) => {
          state.mods = state.mods.map((mod: ModInfo) => {
            if (mod.sha === sha) {
              return { ...mod, isLoaded: true, hasCache: true };
            }
            if (unloadedShas.includes(mod.sha)) {
              return { ...mod, isLoaded: false };
            }
            return mod;
          });
          // Update selectedMod if it's the loaded mod
          if (state.selectedMod?.sha === sha) {
            state.selectedMod = { ...state.selectedMod, isLoaded: true, hasCache: true };
          }
          // Update selectedMod if it's one of the unloaded mods
          if (state.selectedMod && unloadedShas.includes(state.selectedMod.sha)) {
            state.selectedMod = { ...state.selectedMod, isLoaded: false };
          }
          // Also update Category filtered mods if present
          if (state.CategoryFilteredMods) {
            state.CategoryFilteredMods = state.CategoryFilteredMods.map((mod: ModInfo) => {
              if (mod.sha === sha) {
                return { ...mod, isLoaded: true, hasCache: true };
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
          // Update selectedMod if it's the unloaded mod
          if (state.selectedMod?.sha === sha) {
            state.selectedMod = { ...state.selectedMod, isLoaded: false };
          }
          // Also update Category filtered mods if present
          if (state.CategoryFilteredMods) {
            state.CategoryFilteredMods = state.CategoryFilteredMods.map((mod: ModInfo) =>
              mod.sha === sha ? { ...mod, isLoaded: false } : mod
            );
          }
        }),

      optimisticCategoryUpdate: (sha, categoryId) =>
        set((state) => {
          state.mods = state.mods.map((mod: ModInfo) =>
            mod.sha === sha ? { ...mod, category: categoryId } : mod
          );
          // Also update Category filtered mods if present
          if (state.CategoryFilteredMods) {
            state.CategoryFilteredMods = state.CategoryFilteredMods.map((mod: ModInfo) =>
              mod.sha === sha ? { ...mod, category: categoryId } : mod
            );
          }
        }),

      // ============================================================
      // Statistics Actions
      // ============================================================

      setStatistics: (statistics) =>
        set((state) => {
          state.statistics = statistics;
        }),

      // ============================================================
      // Category Actions
      // ============================================================

      setCategoryTree: (tree) =>
        set((state) => {
          state.CategoryTree = tree;
        }),

      setCategoryLoading: (loading) =>
        set((state) => {
          state.CategoryLoading = loading;
        }),

      setPreviewLoading: (loading) =>
        set((state) => {
          state.previewLoading = loading;
        }),

      setPreviewPaths: (paths) =>
        set((state) => {
          state.previewPaths = paths;
        }),

      bustPreviewCache: () =>
        set((state) => {
          state.previewCacheTimestamp = Date.now();
        }),

      setSelectedCategory: (node) =>
        set((state) => {
          state.selectedCategory = node;
        }),

      setCategoryFilteredMods: (mods) =>
        set((state) => {
          state.CategoryFilteredMods = mods;
        }),

      setcategorySearch: (search) =>
        set((state) => {
          state.categorySearch = search;
        }),

      clearCategoryFilter: () =>
        set((state) => {
          state.selectedCategory = undefined;
          state.CategoryFilteredMods = undefined;
        }),

      // ============================================================
      // UI Actions
      // ============================================================

      setExpandedKeys: (keys) =>
        set((state) => {
          state.expandedKeys = keys;
        }),

      setLockedCategories: (keys) =>
        set((state) => {
          state.lockedCategories = keys;
        }),

      addLockedCategory: (key) =>
        set((state) => {
          if (!state.lockedCategories.includes(key)) {
            state.lockedCategories.push(key);
          }
        }),

      removeLockedCategory: (key) =>
        set((state) => {
          state.lockedCategories = state.lockedCategories.filter((k) => k !== key);
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
      // Panel Size Actions
      // ============================================================

      setPanelSizes: (sizes) =>
        set((state) => {
          state.panelSizes = sizes;
        }),

      // ============================================================
      // Import Workflow Screen Actions
      // ============================================================

      openImportWorkflowScreen: () =>
        set((state) => {
          state.importWorkflowScreenVisible = true;
        }),

      closeImportWorkflowScreen: () =>
        set((state) => {
          state.importWorkflowScreenVisible = false;
        }),

      // ============================================================
      // Global Actions
      // ============================================================

      reset: () => set(initialState),
    }))
);

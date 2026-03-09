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
import { current } from 'immer';
import { ModInfo, ModStatistics } from '../../../shared/types/mod.types';
import { CategoryInfo } from '../../../shared/types/category.types';

// ============================================================================
// State Interface
// ============================================================================

export type ModListViewMode = 'category' | 'unclassified' | 'all' | 'loaded';

export interface ModsState {
  // Profile tracking for state preservation
  profileId?: string;

  // Mod List Panel
  selectedMod: ModInfo | undefined;
  selectedMods: ModInfo[];
  mods: ModInfo[] | undefined; // Current mods list (filtered by view mode)
  modLoading: boolean; // Loading state for mod list operations (update, delete, refresh)
  viewMode: ModListViewMode; // Current view mode for mod list

  // Statistics (global mod stats - not affected by category selection)
  statistics: ModStatistics | undefined;

  // Category Panel
  categoryTree: CategoryInfo[];
  categoryLoading: boolean; // Loading state for Category tree panel
  selectedCategory: CategoryInfo | undefined;
  categorySearch: string;
  unclassifiedCount: number; // Count of mods without category assignment

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
  } | undefined;

  // Import Workflow Screen
  importWorkflowScreenVisible: boolean;

  // Batch Edit Screen
  batchEditScreenVisible: boolean;
  modsToEdit: ModInfo[];
}

// ============================================================================
// Actions Interface
// ============================================================================

export interface ModsActions {
  // Mod List Panel Actions
  setSelectedMod: (mod: ModInfo | undefined) => void;
  setSelectedMods: (mods: ModInfo[]) => void;
  setMods: (mods: ModInfo[] | undefined) => void;
  setModLoading: (loading: boolean) => void;
  setViewMode: (mode: ModListViewMode) => void;
  updateModLocal: (sha: string, data: Partial<ModInfo>) => void;
  removeMod: (sha: string) => void;

  // Statistics Actions
  setStatistics: (statistics: ModStatistics) => void;

  // Category Panel Actions
  setCategoryTree: (tree: CategoryInfo[]) => void;
  setCategoryLoading: (loading: boolean) => void;
  setSelectedCategory: (node: CategoryInfo | undefined) => void;
  setUnclassifiedCount: (count: number) => void;

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

  // Batch Edit Screen Actions
  openBatchEditScreen: (mods: ModInfo[]) => void;
  closeBatchEditScreen: () => void;

  // Global Actions
  reset: (newProfileId?: string) => void;
}

export type ModsStore = ModsState & ModsActions;

// ============================================================================
// Per-Profile State Cache
// ============================================================================

interface ProfileUIState {
  selectedCategory?: CategoryInfo;
  selectedMod?: ModInfo;
  expandedKeys: React.Key[];
  lockedCategories: string[];
  searchQuery: string;
  viewMode: ModListViewMode;
  panelSizes?: {
    categoryWidth: number;
    modListWidth: number;
    previewWidth: number;
  };
}

// Cache UI state per profile for seamless switching between profiles
const profileStateCache = new Map<string, ProfileUIState>();

// ============================================================================
// Initial State
// ============================================================================

const initialState: ModsState = {
  // Profile tracking
  profileId: undefined,

  // Mod List Panel
  selectedMod: undefined,
  selectedMods: [],
  mods: undefined,
  modLoading: false,
  viewMode: 'category', // Default to category view

  // Statistics
  statistics: undefined,

  // Category Panel
  categoryTree: [],
  categoryLoading: false,
  selectedCategory: undefined,
  categorySearch: '',
  unclassifiedCount: 0,

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

  // Panel Sizes (undefined until loaded from backend)
  panelSizes: undefined,

  // Import Workflow Screen
  importWorkflowScreenVisible: false,

  // Batch Edit Screen
  batchEditScreenVisible: false,
  modsToEdit: [],
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

      setSelectedMod: (mod) =>
        set((state) => {
          state.selectedMod = mod;
        }),

      setSelectedMods: (mods) =>
        set((state) => {
          state.selectedMods = mods;
        }),

      setMods: (mods) =>
        set((state) => {
          state.mods = mods;
        }),

      setModLoading: (loading) =>
        set((state) => {
          state.modLoading = loading;
        }),

      setViewMode: (mode) =>
        set((state) => {
          state.viewMode = mode;
        }),

      updateModLocal: (sha, data) =>
        set((state) => {
          // Update selectedMod if it matches
          if (state.selectedMod?.sha === sha) {
            state.selectedMod = { ...state.selectedMod, ...data };
          }
          // Update mods list if present
          if (state.mods) {
            state.mods = state.mods.map((mod: ModInfo) =>
              mod.sha === sha ? { ...mod, ...data } : mod
            );
          }
        }),

      removeMod: (sha) =>
        set((state) => {
          // Clear selectedMod if it matches
          if (state.selectedMod?.sha === sha) {
            state.selectedMod = undefined;
          }
          // Remove from selectedMods
          state.selectedMods = state.selectedMods.filter((mod: ModInfo) => mod.sha !== sha);
          // Remove from mods list if present
          if (state.mods) {
            state.mods = state.mods.filter(
              (mod: ModInfo) => mod.sha !== sha
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
          state.categoryTree = tree;
        }),

      setCategoryLoading: (loading) =>
        set((state) => {
          state.categoryLoading = loading;
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

      setUnclassifiedCount: (count) =>
        set((state) => {
          state.unclassifiedCount = count;
        }),

      setcategorySearch: (search) =>
        set((state) => {
          state.categorySearch = search;
        }),

      clearCategoryFilter: () =>
        set((state) => {
          state.selectedCategory = undefined;
          state.mods = undefined;
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

      openBatchEditScreen: (mods: ModInfo[]) =>
        set((state) => {
          state.batchEditScreenVisible = true;
          state.modsToEdit = mods;
        }),

      closeBatchEditScreen: () =>
        set((state) => {
          state.batchEditScreenVisible = false;
          state.modsToEdit = [];
        }),

      // ============================================================
      // Global Actions
      // ============================================================

      reset: (newProfileId?: string) =>
        set((state) => {
          // Step 1: Save current profile's UI state before resetting
          // Use current() to extract plain values from Immer draft proxies
          if (state.profileId) {
            profileStateCache.set(state.profileId, {
              selectedCategory: state.selectedCategory ? current(state.selectedCategory) : undefined,
              selectedMod: state.selectedMod ? current(state.selectedMod) : undefined,
              expandedKeys: current(state.expandedKeys),
              lockedCategories: current(state.lockedCategories),
              searchQuery: state.searchQuery,
              viewMode: state.viewMode,
              panelSizes: state.panelSizes ? current(state.panelSizes) : undefined,
            });
          }

          // Step 2: Reset to initial state
          Object.assign(state, initialState);

          // Step 3: Set new profile ID
          state.profileId = newProfileId;

          // Step 4: Restore cached UI state for new profile if available
          if (newProfileId && profileStateCache.has(newProfileId)) {
            const cached = profileStateCache.get(newProfileId)!;
            state.selectedCategory = cached.selectedCategory;
            state.selectedMod = cached.selectedMod;
            state.expandedKeys = cached.expandedKeys;
            state.lockedCategories = cached.lockedCategories;
            state.searchQuery = cached.searchQuery;
            state.viewMode = cached.viewMode;
            state.panelSizes = cached.panelSizes;
          }
        }),
    }))
);

/**
 * Selectors for derived/computed mod state
 * Keeps components clean and enables memoization
 */

import { ModsState } from '../modsStore';
import { ModInfo, ModStatistics } from '../../../../shared/types/mod.types';

/**
 * Get all mods
 */
export const selectMods = (state: ModsState): ModInfo[] => state.mods;

/**
 * Get filtered mods based on search, object filter, and Category
 */
export const selectFilteredMods = (state: ModsState): ModInfo[] => {
  let filtered = state.mods;

  // Apply Category filter first (if active)
  if (state.CategoryFilteredMods !== undefined) {
    filtered = state.CategoryFilteredMods;
  }

  // Apply object filter
  if (state.selectedObject && state.selectedObject !== '') {
    filtered = filtered.filter((mod) => mod.type === state.selectedObject);
  }

  // Apply search query
  if (state.searchQuery && state.searchQuery.trim() !== '') {
    const query = state.searchQuery.toLowerCase().trim();
    filtered = filtered.filter(
      (mod) =>
        mod.name.toLowerCase().includes(query) ||
        mod.author.toLowerCase().includes(query) ||
        mod.description.toLowerCase().includes(query) ||
        mod.tags.some((tag) => tag.toLowerCase().includes(query))
    );
  }

  return filtered;
};

/**
 * Get mod statistics
 */
export const selectModStatistics = (state: ModsState): ModStatistics => {
  const filteredMods = selectFilteredMods(state);

  return {
    totalMods: filteredMods.length,
    loadedMods: filteredMods.filter((mod) => mod.isLoaded).length,
    availableMods: filteredMods.filter((mod) => mod.isAvailable).length,
  };
};

/**
 * Get mod by SHA
 */
export const selectModBySha = (state: ModsState, sha: string): ModInfo | undefined => {
  return state.mods.find((mod) => mod.sha === sha);
};

/**
 * Check if any mods are loaded
 */
export const selectHasLoadedMods = (state: ModsState): boolean => {
  return state.mods.some((mod) => mod.isLoaded);
};

/**
 * Get loaded mods
 */
export const selectLoadedMods = (state: ModsState): ModInfo[] => {
  return state.mods.filter((mod) => mod.isLoaded);
};

/**
 * Get mods by category
 */
export const selectModsByCategory = (
  state: ModsState,
  categoryId: string
): ModInfo[] => {
  return state.mods.filter((mod) => mod.category === categoryId);
};

/**
 * Check if selection is active
 */
export const selectHasSelection = (state: ModsState): boolean => {
  return state.selectedMods.length > 0;
};

/**
 * Check if multiple mods are selected
 */
export const selectHasMultipleSelection = (state: ModsState): boolean => {
  return state.selectedMods.length > 1;
};

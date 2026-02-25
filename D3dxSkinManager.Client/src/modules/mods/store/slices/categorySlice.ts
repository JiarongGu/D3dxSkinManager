/**
 * Category slice - manages Category tree state and filtering
 */

import { CategoryInfo } from '../../../../shared/types/category.types';
import { ModInfo } from '../../../../shared/types/mod.types';

export interface CategoriesliceState {
  CategoryTree: CategoryInfo[];
  CategoryLoading: boolean;
  selectedCategory: CategoryInfo | undefined;
  CategoryFilteredMods: ModInfo[] | undefined;
  categorySearch: string;
}

export const initialCategoriestate: CategoriesliceState = {
  CategoryTree: [],
  CategoryLoading: false,
  selectedCategory: undefined,
  CategoryFilteredMods: undefined,
  categorySearch: '',
};

export interface CategoriesliceActions {
  setCategoryTree: (tree: CategoryInfo[]) => void;
  setCategoryLoading: (loading: boolean) => void;
  setSelectedCategory: (node: CategoryInfo | undefined) => void;
  setCategoryFilteredMods: (mods: ModInfo[] | undefined) => void;
  setcategorySearch: (search: string) => void;

  // Local tree updates (for optimistic updates)
  updateTreeNodeLocal: (nodeId: string, updates: Partial<CategoryInfo>) => void;

  // Clear filter
  clearCategoryFilter: () => void;

  // Reset
  reset: () => void;
}

export const createCategoriesliceActions = (
  set: (fn: (state: CategoriesliceState) => Partial<CategoriesliceState>) => void,
  get: () => CategoriesliceState
): CategoriesliceActions => ({
  setCategoryTree: (tree) => set(() => ({ CategoryTree: tree })),

  setCategoryLoading: (loading) => set(() => ({ CategoryLoading: loading })),

  setSelectedCategory: (node) => set(() => ({ selectedCategory: node })),

  setCategoryFilteredMods: (mods) => set(() => ({ CategoryFilteredMods: mods })),

  setcategorySearch: (search) => set(() => ({ categorySearch: search })),

  updateTreeNodeLocal: (nodeId, updates) =>
    set((state) => {
      const updateNode = (nodes: CategoryInfo[]): CategoryInfo[] => {
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

      return {
        CategoryTree: updateNode(state.CategoryTree),
      };
    }),

  clearCategoryFilter: () =>
    set(() => ({
      selectedCategory: undefined,
      CategoryFilteredMods: undefined,
    })),

  reset: () => set(() => initialCategoriestate),
});

/**
 * Selectors for Category tree operations
 */

import { ModsState } from '../modsStore';
import { CategoryInfo } from '../../../../shared/types/category.types';

/**
 * Get Category tree
 */
export const selectCategoryTree = (state: ModsState): CategoryInfo[] =>
  state.CategoryTree;

/**
 * Find node by ID in tree
 */
export const selectCategoryInfoById = (
  state: ModsState,
  nodeId: string
): CategoryInfo | undefined => {
  const findNode = (nodes: CategoryInfo[]): CategoryInfo | undefined => {
    for (const node of nodes) {
      if (node.id === nodeId) return node;
      if (node.children && node.children.length > 0) {
        const found = findNode(node.children);
        if (found) return found;
      }
    }
    return undefined;
  };

  return findNode(state.CategoryTree);
};

/**
 * Get all node IDs (for tree expansion)
 */
export const selectAllNodeIds = (state: ModsState): string[] => {
  const ids: string[] = [];

  const collectIds = (nodes: CategoryInfo[]) => {
    nodes.forEach((node) => {
      ids.push(node.id);
      if (node.children && node.children.length > 0) {
        collectIds(node.children);
      }
    });
  };

  collectIds(state.CategoryTree);
  return ids;
};

/**
 * Check if Category filter is active
 */
export const selectIsCategoryFilterActive = (state: ModsState): boolean => {
  return state.selectedCategory !== undefined;
};

/**
 * Get path to selected Category (breadcrumb)
 */
export const selectCategoryPath = (state: ModsState): CategoryInfo[] => {
  if (!state.selectedCategory) return [];

  const path: CategoryInfo[] = [];
  const targetId = state.selectedCategory.id;

  const findPath = (
    nodes: CategoryInfo[],
    currentPath: CategoryInfo[]
  ): boolean => {
    for (const node of nodes) {
      const newPath = [...currentPath, node];

      if (node.id === targetId) {
        path.push(...newPath);
        return true;
      }

      if (node.children && node.children.length > 0) {
        if (findPath(node.children, newPath)) {
          return true;
        }
      }
    }
    return false;
  };

  findPath(state.CategoryTree, []);
  return path;
};

/**
 * Get filtered search results from tree
 */
export const selectFilteredCategoryTree = (
  state: ModsState
): CategoryInfo[] => {
  if (!state.categorySearch || state.categorySearch.trim() === '') {
    return state.CategoryTree;
  }

  const query = state.categorySearch.toLowerCase();

  const filterNodes = (nodes: CategoryInfo[]): CategoryInfo[] => {
    return nodes
      .map((node) => {
        const matchesSearch = node.name.toLowerCase().includes(query);
        const filteredChildren = node.children ? filterNodes(node.children) : [];

        // Include if node matches or has matching children
        if (matchesSearch || filteredChildren.length > 0) {
          return {
            ...node,
            children: filteredChildren,
          };
        }

        return null;
      })
      .filter((node): node is CategoryInfo => node !== null);
  };

  return filterNodes(state.CategoryTree);
};

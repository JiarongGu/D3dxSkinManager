/**
 * Selectors for classification tree operations
 */

import { ModsState } from '../modsStore';
import { ClassificationNode } from '../../../../shared/types/classification.types';

/**
 * Get classification tree
 */
export const selectClassificationTree = (state: ModsState): ClassificationNode[] =>
  state.classificationTree;

/**
 * Find node by ID in tree
 */
export const selectClassificationNodeById = (
  state: ModsState,
  nodeId: string
): ClassificationNode | undefined => {
  const findNode = (nodes: ClassificationNode[]): ClassificationNode | undefined => {
    for (const node of nodes) {
      if (node.id === nodeId) return node;
      if (node.children && node.children.length > 0) {
        const found = findNode(node.children);
        if (found) return found;
      }
    }
    return undefined;
  };

  return findNode(state.classificationTree);
};

/**
 * Get all node IDs (for tree expansion)
 */
export const selectAllNodeIds = (state: ModsState): string[] => {
  const ids: string[] = [];

  const collectIds = (nodes: ClassificationNode[]) => {
    nodes.forEach((node) => {
      ids.push(node.id);
      if (node.children && node.children.length > 0) {
        collectIds(node.children);
      }
    });
  };

  collectIds(state.classificationTree);
  return ids;
};

/**
 * Check if classification filter is active
 */
export const selectIsClassificationFilterActive = (state: ModsState): boolean => {
  return state.selectedClassification !== undefined;
};

/**
 * Get path to selected classification (breadcrumb)
 */
export const selectClassificationPath = (state: ModsState): ClassificationNode[] => {
  if (!state.selectedClassification) return [];

  const path: ClassificationNode[] = [];
  const targetId = state.selectedClassification.id;

  const findPath = (
    nodes: ClassificationNode[],
    currentPath: ClassificationNode[]
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

  findPath(state.classificationTree, []);
  return path;
};

/**
 * Get filtered search results from tree
 */
export const selectFilteredClassificationTree = (
  state: ModsState
): ClassificationNode[] => {
  if (!state.classificationSearch || state.classificationSearch.trim() === '') {
    return state.classificationTree;
  }

  const query = state.classificationSearch.toLowerCase();

  const filterNodes = (nodes: ClassificationNode[]): ClassificationNode[] => {
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
      .filter((node): node is ClassificationNode => node !== null);
  };

  return filterNodes(state.classificationTree);
};

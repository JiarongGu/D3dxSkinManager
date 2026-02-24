/**
 * Classification slice - manages classification tree state and filtering
 */

import { ClassificationNode } from '../../../../shared/types/classification.types';
import { ModInfo } from '../../../../shared/types/mod.types';

export interface ClassificationSliceState {
  classificationTree: ClassificationNode[];
  classificationLoading: boolean;
  selectedClassification: ClassificationNode | undefined;
  classificationFilteredMods: ModInfo[] | undefined;
  classificationSearch: string;
}

export const initialClassificationState: ClassificationSliceState = {
  classificationTree: [],
  classificationLoading: false,
  selectedClassification: undefined,
  classificationFilteredMods: undefined,
  classificationSearch: '',
};

export interface ClassificationSliceActions {
  setClassificationTree: (tree: ClassificationNode[]) => void;
  setClassificationLoading: (loading: boolean) => void;
  setSelectedClassification: (node: ClassificationNode | undefined) => void;
  setClassificationFilteredMods: (mods: ModInfo[] | undefined) => void;
  setClassificationSearch: (search: string) => void;

  // Local tree updates (for optimistic updates)
  updateTreeNodeLocal: (nodeId: string, updates: Partial<ClassificationNode>) => void;

  // Clear filter
  clearClassificationFilter: () => void;

  // Reset
  reset: () => void;
}

export const createClassificationSliceActions = (
  set: (fn: (state: ClassificationSliceState) => Partial<ClassificationSliceState>) => void,
  get: () => ClassificationSliceState
): ClassificationSliceActions => ({
  setClassificationTree: (tree) => set(() => ({ classificationTree: tree })),

  setClassificationLoading: (loading) => set(() => ({ classificationLoading: loading })),

  setSelectedClassification: (node) => set(() => ({ selectedClassification: node })),

  setClassificationFilteredMods: (mods) => set(() => ({ classificationFilteredMods: mods })),

  setClassificationSearch: (search) => set(() => ({ classificationSearch: search })),

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

      return {
        classificationTree: updateNode(state.classificationTree),
      };
    }),

  clearClassificationFilter: () =>
    set(() => ({
      selectedClassification: undefined,
      classificationFilteredMods: undefined,
    })),

  reset: () => set(() => initialClassificationState),
});

/**
 * Classification operations - managing classification tree and filtering
 */

import { useModsStore } from '../store/modsStore';
import { modService } from '../services/modService';
import { classificationService } from '../../../shared/services/classificationService';
import { handleError } from '../../../shared/utils/errorHandler';
import { executeWithDelayedLoading } from '../../../shared/utils/delayedLoading';

/**
 * Load classification tree
 * Uses delayed loading (100ms) to avoid flicker for fast loads
 */
export async function loadClassificationTree(profileId: string): Promise<void> {
  const { setClassificationLoading, setClassificationTree } = useModsStore.getState();

  try {
    await executeWithDelayedLoading(
      async () => {
        const tree = await classificationService.getClassificationTree(profileId);
        setClassificationTree(tree);
      },
      setClassificationLoading,
      100
    );
  } catch (error) {
    handleError(error);
  }
}

/**
 * Refresh classification tree
 */
export async function refreshClassificationTree(profileId: string): Promise<void> {
  await loadClassificationTree(profileId);
}

/**
 * Load mods filtered by classification node
 * Uses delayed loading (100ms) to avoid flicker for fast queries
 */
export async function loadModsByClassification(
  profileId: string,
  nodeId: string
): Promise<void> {
  const { setClassificationLoading, setClassificationFilteredMods } = useModsStore.getState();

  try {
    await executeWithDelayedLoading(
      async () => {
        const mods = await modService.getModsByClassification(profileId, nodeId);
        setClassificationFilteredMods(mods);
      },
      setClassificationLoading,
      100
    );
  } catch (error) {
    handleError(error);
  }
}

/**
 * Load unclassified mods
 * Uses delayed loading (100ms) to avoid flicker for fast queries
 */
export async function loadUnclassifiedMods(profileId: string): Promise<void> {
  const { setClassificationLoading, setClassificationFilteredMods } = useModsStore.getState();

  try {
    await executeWithDelayedLoading(
      async () => {
        const mods = await modService.getUnclassifiedMods(profileId);
        setClassificationFilteredMods(mods);
      },
      setClassificationLoading,
      100
    );
  } catch (error) {
    handleError(error);
  }
}

/**
 * Clear classification filter
 */
export function clearClassificationFilter(): void {
  useModsStore.getState().clearClassificationFilter();
}

/**
 * Select classification node and load its mods
 */
export async function selectClassification(
  profileId: string,
  nodeId: string
): Promise<void> {
  const state = useModsStore.getState();

  // Find the node in the tree
  const findNode = (nodes: typeof state.classificationTree): typeof state.selectedClassification => {
    for (const node of nodes) {
      if (node.id === nodeId) return node;
      if (node.children) {
        const found = findNode(node.children);
        if (found) return found;
      }
    }
    return undefined;
  };

  const node = findNode(state.classificationTree);
  state.setSelectedClassification(node);

  // Load mods for this classification
  if (nodeId === '__unclassified__') {
    await loadUnclassifiedMods(profileId);
  } else {
    await loadModsByClassification(profileId, nodeId);
  }
}

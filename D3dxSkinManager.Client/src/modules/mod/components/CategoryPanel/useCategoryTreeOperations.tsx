import { useCallback, useState } from "react";
import { App } from "antd";
import { CategoryInfo, CATEGORY_IDS } from "../../../../shared/types/category.types";
import { categoryService } from "../../../../shared/services/ipc";
import { useProfile } from "../../../../shared/context/ProfileContext";
import { useModCategoryUpdate } from "./useModCategoryUpdate";
import { notification } from "../../../../shared/utils/notification";
import { useStableRef } from "../../../../shared/hooks/useStableRef";
import { useCategoryScreen } from "./CategoryScreen";
import { useTranslation } from "react-i18next";

/**
 * Find a CategoryInfo by ID in the tree
 */
function findNodeById(
  nodes: CategoryInfo[],
  id: string,
): CategoryInfo | undefined {
  for (const node of nodes) {
    if (node.id === id) return node;
    if (node.children.length > 0) {
      const found = findNodeById(node.children, id);
      if (found) return found;
    }
  }
  return undefined;
}

interface UseCategoryTreeOperationsProps {
  tree: CategoryInfo[];
  expandedKeys: React.Key[];
  selectedCategoryId?: string;
  onExpandedKeysChange: (keys: React.Key[]) => void;
}

/**
 * Custom hook for handling Category tree operations
 * (edit, delete, drag & drop with delayed loading)
 */
export function useCategoryTreeOperations({
  tree,
  expandedKeys,
  selectedCategoryId,
  onExpandedKeysChange,
}: UseCategoryTreeOperationsProps) {
  const { modal } = App.useApp();
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();
  const { updateModCategory, updateModsCategory } = useModCategoryUpdate();
  const { openCategoryScreen } = useCategoryScreen();

  const [treeRef, selectedProfileIdRef] = useStableRef(tree, selectedProfileId);

  // State for delete confirmation dialog
  const [deleteConfirmation, setDeleteConfirmation] = useState<{
    visible: boolean;
    nodeId: string;
    nodeName: string;
    hasChildren: boolean;
  }>({ visible: false, nodeId: '', nodeName: '', hasChildren: false });

  // Edit node handler - now uses slide-in screen instead of modal
  const handleEditNode = useCallback(
    async (nodeId: string) => {
      const node = findNodeById(treeRef.current, nodeId);
      if (!node) return;

      openCategoryScreen({
        tree: treeRef.current,
        editNode: node,
        onSave: async (data) => {
          if (!selectedProfileIdRef.current) {
            notification.error(t('errors.noProfileSelected'));
            return;
          }

          try {
            // With stable IDs, we don't need to worry about cascading updates
            // Just check for name uniqueness (handled by backend)

            // Use the thumbnail from form data directly (no confirmation needed)
            const thumbnailToUse = data.thumbnail;

            // Check if parent has changed
            const parentChanged = data.parentId !== node.parentId;

            // Perform the update
            const response = await categoryService.updateCategory(
              selectedProfileIdRef.current,
              nodeId,
              data.name,
              data.description,
              thumbnailToUse
            );

            // If parent changed, also move the node
            let moveSuccess = true;
            if (response && parentChanged) {
              moveSuccess = await categoryService.moveCategory(
                selectedProfileIdRef.current,
                nodeId,
                data.parentId,
                -1 // No specific drop position for edit operation (-1 means append at end)
              );
            }

            if (response && moveSuccess) {
              notification.success(t('category.updateSuccess', { name: data.name }));
              // Note: Backend emits CATEGORY_UPDATED event, ModProvider handles refresh automatically
            } else if (response && !moveSuccess && parentChanged) {
              notification.error(t('category.moveError', { name: data.name }));
            } else {
              notification.error(t('category.updateFailed', { name: data.name }));
            }
          } catch (error: unknown) {
                        if (error instanceof Error && error.message !== 'User cancelled') {
              notification.error(t('category.updateError', {
                error: error.message || 'Unknown error'
              }));
            }
          }
        },
      });
    },
    [openCategoryScreen, modal, t],
  );

  // Delete node handler - opens confirmation dialog
  const handleDeleteNode = useCallback(
    async (nodeId: string) => {
      const node = findNodeById(treeRef.current, nodeId);
      if (!node) return;

      const hasChildren = node.children && node.children.length > 0;

      // Open the confirmation dialog
      setDeleteConfirmation({
        visible: true,
        nodeId,
        nodeName: node.name,
        hasChildren
      });
    },
    [],
  );

  // Handle the actual deletion after confirmation
  const handleDeleteConfirm = useCallback(async () => {
    const { nodeId } = deleteConfirmation;
    if (!selectedProfileIdRef.current || !nodeId) return;

    try {
      const response = await categoryService.deleteCategory(
        selectedProfileIdRef.current,
        nodeId
      );

      if (response) {
        notification.success(t('category.deleteSuccess'));
        setDeleteConfirmation({ visible: false, nodeId: '', nodeName: '', hasChildren: false });
      } else {
        notification.error(t('category.deleteFailed'));
      }
    } catch (error: unknown) {
            notification.error(t('category.deleteError'));
    }
  }, [deleteConfirmation.nodeId, t]);

  // Simplified node reorder handler - just takes node IDs and drop type
  // dropNodeId can be empty string to indicate dropping to root level
  const handleNodeReorder = useCallback(
    async (
      dragNodeId: string,
      dropNodeId: string,
      dropType: 'node' | 'gap',
      gapSide?: 'top' | 'bottom'
    ) => {
      const currentTree = treeRef.current;

      // Prevent dropping on itself
      if (dragNodeId === dropNodeId) {
        return;
      }

      if (!selectedProfileIdRef.current) {
        return;
      }

      try {
        // Handle dropping to root level (empty dropNodeId)
        if (dropNodeId === '') {
          await categoryService.moveCategory(
            selectedProfileIdRef.current,
            dragNodeId,
            undefined, // undefined parent = root level
            0
          );
          return;
        }

        // Expand target node if dropping into it
        if (dropType === 'node' && !expandedKeys.includes(dropNodeId)) {
          onExpandedKeysChange([...expandedKeys, dropNodeId]);
        }

        // Check if dropping into a node or between nodes
        if (dropType === 'gap') {
          // Dropping between nodes (reordering or moving to root)
          const dropNode = findNodeById(currentTree, dropNodeId);

          // Determine the parent: if dropNode has no parent, we're at root level
          const newParentId = dropNode ? dropNode.parentId : undefined;

          // Calculate the actual position within siblings
          let siblings: CategoryInfo[] = [];
          if (newParentId) {
            // Find parent node and get its children
            const parentNode = findNodeById(currentTree, newParentId);
            siblings = parentNode ? parentNode.children : [];
          } else {
            // Root level siblings
            siblings = currentTree;
          }

          // Find the index of the drop target node within siblings
          const dropNodeIndex = siblings.findIndex((s) => s.id === dropNodeId);

          // For gap drops, position depends on whether dropping above or below
          // top: place before (at same index), bottom: place after (index + 1)
          let finalPosition: number;
          if (gapSide === 'top') {
            finalPosition = dropNodeIndex; // Place before the target
          } else {
            finalPosition = dropNodeIndex + 1; // Place after the target (default)
          }

          // Send move request to backend
          await categoryService.moveCategory(
              selectedProfileIdRef.current,
            dragNodeId,
            newParentId,
            Math.max(0, finalPosition)
          );
        } else {
          // Dropping into a node (moving to new parent)
          await categoryService.moveCategory(
            selectedProfileIdRef.current,
            dragNodeId,
            dropNodeId,
            0
          );
        }
      } catch (error: unknown) {
                notification.error(t('category.tree.moveNodeFailed'));
      }
    },
    [expandedKeys, onExpandedKeysChange],
  );


  // Simplified mod Category handler - just takes mod ID and category node ID
  const handleModClassify = useCallback(
    async (modId: string, nodeId: string) => {
      if (!modId) {
        notification.error(t('category.dragDrop.noModSelected'));
        return;
      }

      if (!nodeId) {
        notification.error(t('category.dragDrop.noCategorySelected'));
        return;
      }

      try {
        // Find the node name from the tree
        const findNodeName = (nodes: CategoryInfo[], id: string): string | undefined => {
          for (const node of nodes) {
            if (node.id === id) {
              return node.name;
            }
            if (node.children.length > 0) {
              const found = findNodeName(node.children, id);
              if (found) return found;
            }
          }
          return undefined;
        };

        const nodeName = findNodeName(treeRef.current, nodeId) || nodeId;

        // If moving to "Unclassified", clear the category by passing empty string
        const categoryValue = nodeId === CATEGORY_IDS.UNCLASSIFIED ? '' : nodeId;

        // Update the mod's category using the shared hook
        // Note: modName is optional, pass empty string if not available
        await updateModCategory(modId, categoryValue, nodeName);
      } catch (error: unknown) {
                notification.error(t('category.dragDrop.updateFailed'));
      }
    },
    [updateModCategory]
  );

  // Bulk mod classification handler - takes array of mod IDs and category node ID
  const handleBulkModClassify = useCallback(
    async (modIds: string[], nodeId: string) => {
      if (!modIds || modIds.length === 0) {
        notification.error(t('category.dragDrop.noModsSelected'));
        return;
      }

      if (!nodeId) {
        notification.error(t('category.dragDrop.noCategorySelected'));
        return;
      }

      try {
        // Find the node name from the tree
        const findNodeName = (nodes: CategoryInfo[], id: string): string | undefined => {
          for (const node of nodes) {
            if (node.id === id) {
              return node.name;
            }
            if (node.children.length > 0) {
              const found = findNodeName(node.children, id);
              if (found) return found;
            }
          }
          return undefined;
        };

        const nodeName = findNodeName(treeRef.current, nodeId) || nodeId;

        // If moving to "Unclassified", clear the category by passing empty string
        const categoryValue = nodeId === CATEGORY_IDS.UNCLASSIFIED ? '' : nodeId;

        // Call batch update category
        await updateModsCategory(modIds, categoryValue, nodeName);
      } catch (error: unknown) {
                notification.error(t('category.dragDrop.batchUpdateFailed'));
      }
    },
    [updateModsCategory]
  );

  return {
    handleEditNode,
    handleDeleteNode,
    handleNodeReorder,
    handleModClassify,
    handleBulkModClassify,
    // Expose delete confirmation state and handlers for the component to use
    deleteConfirmation,
    handleDeleteConfirm,
    closeDeleteConfirmation: () => setDeleteConfirmation({ visible: false, nodeId: '', nodeName: '', hasChildren: false })
  };
}

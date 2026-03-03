import { useCallback, useState } from "react";
import { App } from "antd";
import { CategoryInfo, CATEGORY_IDS } from "../../../../shared/types/category.types";
import { categoryService } from "../../../../shared/services/categoryService";
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

/**
 * Check if nodeId is a descendant of ancestorId in the tree
 */
function isDescendantOf(
  nodes: CategoryInfo[],
  nodeId: string,
  ancestorId: string,
): boolean {
  const ancestorNode = findNodeById(nodes, ancestorId);
  if (!ancestorNode) return false;

  // Check if nodeId exists in ancestor's subtree
  return findNodeById(ancestorNode.children, nodeId) !== undefined;
}

/**
 * Check if nodeId is an ancestor of descendantId in the tree
 */
function isAncestorOf(
  nodes: CategoryInfo[],
  nodeId: string,
  descendantId: string,
): boolean {
  return isDescendantOf(nodes, descendantId, nodeId);
}

/**
 * Check if updating a node should trigger a mod list refresh
 * Returns true if:
 * - The updated node is the currently selected node
 * - The updated node is a descendant of the selected node (its mods are shown in current view)
 *
 * Does NOT refresh if updated node is an ancestor (doesn't affect current view)
 */
function shouldRefreshModsForNodeUpdate(
  tree: CategoryInfo[],
  updatedNodeId: string,
  selectedNodeId: string | undefined,
): boolean {
  if (!selectedNodeId) return false;

  // Check if it's the same node
  if (updatedNodeId === selectedNodeId) return true;

  // Check if updated node is a descendant of selected node
  // (i.e., updated node's mods are being shown as part of selected node)
  if (isDescendantOf(tree, updatedNodeId, selectedNodeId)) return true;

  // Do NOT refresh if updated node is an ancestor
  // (that doesn't affect the current mod list view)

  return false;
}

interface UseCategoryTreeOperationsProps {
  tree: CategoryInfo[];
  expandedKeys: React.Key[];
  selectedCategoryId?: string;
  onExpandedKeysChange: (keys: React.Key[]) => void;
  onRefreshTree?: () => Promise<void>;
  onModsRefresh?: () => Promise<void>;
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
  onRefreshTree,
  onModsRefresh,
}: UseCategoryTreeOperationsProps) {
  const { modal } = App.useApp();
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();
  const { updateModCategory } = useModCategoryUpdate({ onRefreshTree });
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

            // Check if thumbnail is being changed
            let thumbnailToUse = data.thumbnail;
            if (data.thumbnail !== node.thumbnail) {
              if (data.thumbnail && node.thumbnail) {
                // Replacing existing thumbnail
                await new Promise<void>((resolve, reject) => {
                  modal.confirm({
                    title: t('category.edit.replaceThumbnail'),
                    content: t('category.edit.replaceThumbnailMessage'),
                    okText: t('common.replace'),
                    cancelText: t('common.keepExisting'),
                    onOk: () => resolve(),
                    onCancel: () => {
                      thumbnailToUse = node.thumbnail || undefined; // Keep existing thumbnail, convert null to undefined
                      resolve();
                    }
                  });
                });
              }
            }

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

              // Refresh the tree to show updated name and/or new parent
              if (onRefreshTree) {
                await onRefreshTree();
              }

              // Only refresh mods if:
              // 1. The name actually changed AND
              // 2. The updated node affects the current mod list view (current node or its descendants)
              const nameChanged = data.name !== node.name;
              if (onModsRefresh && nameChanged && shouldRefreshModsForNodeUpdate(treeRef.current, nodeId, selectedCategoryId)) {
                await onModsRefresh();
              }
            } else if (response && !moveSuccess && parentChanged) {
              notification.error(t('category.moveError', { name: data.name }));
              // Still refresh to show the name/description changes even if move failed
              if (onRefreshTree) {
                await onRefreshTree();
              }
            } else {
              notification.error(t('category.updateFailed', { name: data.name }));
            }
          } catch (error) {
                        if (error instanceof Error && error.message !== 'User cancelled') {
              notification.error(t('category.updateError', {
                error: error.message || 'Unknown error'
              }));
            }
          }
        },
      });
    },
    [openCategoryScreen, onRefreshTree, onModsRefresh, modal, t],
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
        if (onRefreshTree) {
          await onRefreshTree();
        }
      } else {
        notification.error(t('category.deleteFailed'));
      }
    } catch (error) {
            notification.error(t('category.deleteError'));
    }
  }, [deleteConfirmation.nodeId, onRefreshTree, t]);

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

          if (onRefreshTree) {
            await onRefreshTree();
          }
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

        // After backend operation completes, refresh tree
        // The delayed loading in useCategoryData will prevent flicker
        if (onRefreshTree) {
          await onRefreshTree();
        }
      } catch (error) {
                notification.error("Failed to move Category node");
      }
    },
    [expandedKeys, onExpandedKeysChange, onRefreshTree],
  );


  // Simplified mod Category handler - just takes mod SHA and category node ID
  const handleModClassify = useCallback(
    async (modSha: string, nodeId: string) => {
      if (!modSha) {
        notification.error('No mod selected');
        return;
      }

      if (!nodeId) {
        notification.error('No category selected');
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
        await updateModCategory(modSha, categoryValue, nodeName);
      } catch (error) {
                notification.error('Failed to update mod category');
      }
    },
    [updateModCategory]
  );

  return {
    handleEditNode,
    handleDeleteNode,
    handleNodeReorder,
    handleModClassify,
    // Expose delete confirmation state and handlers for the component to use
    deleteConfirmation,
    handleDeleteConfirm,
    closeDeleteConfirmation: () => setDeleteConfirmation({ visible: false, nodeId: '', nodeName: '', hasChildren: false })
  };
}

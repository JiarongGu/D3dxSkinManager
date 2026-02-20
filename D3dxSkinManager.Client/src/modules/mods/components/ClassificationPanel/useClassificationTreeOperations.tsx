import { useCallback, useRef, useEffect } from "react";
import { App } from "antd";
import { ExclamationCircleOutlined } from "@ant-design/icons";
import { Input } from "antd";
import { ClassificationNode } from "../../../../shared/types/classification.types";
import { classificationService } from "../../../../shared/services/classificationService";
import { useProfile } from "../../../../shared/context/ProfileContext";
import { useModCategoryUpdate } from "./useModCategoryUpdate";
import { notification } from "../../../../shared/utils/notification";
import { useStableRef } from "../../../../shared/hooks/useStableRef";

/**
 * Find a ClassificationNode by ID in the tree
 */
function findNodeById(
  nodes: ClassificationNode[],
  id: string,
): ClassificationNode | null {
  for (const node of nodes) {
    if (node.id === id) return node;
    if (node.children.length > 0) {
      const found = findNodeById(node.children, id);
      if (found) return found;
    }
  }
  return null;
}

interface UseClassificationTreeOperationsProps {
  tree: ClassificationNode[];
  expandedKeys: React.Key[];
  onExpandedKeysChange: (keys: React.Key[]) => void;
  onRefreshTree?: () => Promise<void>;
}

/**
 * Custom hook for handling classification tree operations
 * (edit, delete, drag & drop with delayed loading)
 */
export function useClassificationTreeOperations({
  tree,
  expandedKeys,
  onExpandedKeysChange,
  onRefreshTree,
}: UseClassificationTreeOperationsProps) {
  const { modal } = App.useApp();
  const { selectedProfileId } = useProfile();
  const { updateModCategory } = useModCategoryUpdate({ onRefreshTree });

  const [treeRef, selectedProfileIdRef] = useStableRef(tree, selectedProfileId);

  // Edit node handler
  const handleEditNode = useCallback(
    async (nodeId: string) => {
      const node = findNodeById(treeRef.current, nodeId);
      if (!node) return;

      modal.confirm({
        title: "Edit Classification",
        icon: <ExclamationCircleOutlined />,
        centered: true,
        content: (
          <div style={{ marginTop: "16px" }}>
            <label style={{ display: "block", marginBottom: "8px" }}>
              Name:
            </label>
            <Input id="edit-name-input" defaultValue={node.name} />
          </div>
        ),
        onOk: async () => {
          const input = document.getElementById(
            "edit-name-input",
          ) as HTMLInputElement;
          const newName = input?.value?.trim();

          if (!newName) {
            notification.error("Name cannot be empty");
            return Promise.reject();
          }

          if (!selectedProfileIdRef.current) return;

          try {
            const response = await classificationService.updateNode(
              selectedProfileIdRef.current,
              nodeId,
              newName
            );

            if (response) {
              notification.success("Classification updated successfully");
              if (onRefreshTree) {
                await onRefreshTree();
              }
            } else {
              notification.error("Failed to update classification");
            }
          } catch (error) {
            console.error("Error updating classification:", error);
            notification.error("Failed to update classification");
          }
        },
      });
    },
    [modal, onRefreshTree],
  );

  // Delete node handler
  const handleDeleteNode = useCallback(
    async (nodeId: string) => {
      const node = findNodeById(treeRef.current, nodeId);
      if (!node) return;

      const hasChildren = node.children && node.children.length > 0;
      const warningMessage = hasChildren
        ? `Are you sure you want to delete "${node.name}" and all its sub-classifications?`
        : `Are you sure you want to delete "${node.name}"?`;

      modal.confirm({
        title: "Delete Classification",
        icon: <ExclamationCircleOutlined />,
        centered: true,
        content: warningMessage,
        okText: "Delete",
        okType: "danger",
        cancelText: "Cancel",
        onOk: async () => {
          if (!selectedProfileIdRef.current) return;

          try {
            const response = await classificationService.deleteNode(
              selectedProfileIdRef.current,
              nodeId
            );

            if (response) {
              notification.success("Classification deleted successfully");
              if (onRefreshTree) {
                await onRefreshTree();
              }
            } else {
              notification.error("Failed to delete classification");
            }
          } catch (error) {
            console.error("Error deleting classification:", error);
            notification.error("Failed to delete classification");
          }
        },
      });
    },
    [modal, onRefreshTree],
  );

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
          await classificationService.moveNode(
            selectedProfileIdRef.current,
            dragNodeId,
            null, // null parent = root level
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
          const newParentId = dropNode ? dropNode.parentId || null : null;

          // Calculate the actual position within siblings
          let siblings: ClassificationNode[] = [];
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
          await classificationService.moveNode(
              selectedProfileIdRef.current,
            dragNodeId,
            newParentId,
            Math.max(0, finalPosition)
          );
        } else {
          // Dropping into a node (moving to new parent)
          await classificationService.moveNode(
            selectedProfileIdRef.current,
            dragNodeId,
            dropNodeId,
            0
          );
        }

        // After backend operation completes, refresh tree
        // The delayed loading in useClassificationData will prevent flicker
        if (onRefreshTree) {
          await onRefreshTree();
        }
      } catch (error) {
        console.error("[handleNodeReorder] Error reordering classification node:", error);
        notification.error("Failed to move classification node");
      }
    },
    [expandedKeys, onExpandedKeysChange, onRefreshTree],
  );


  // Simplified mod classification handler - just takes mod SHA and category node ID
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
        const findNodeName = (nodes: ClassificationNode[], id: string): string | null => {
          for (const node of nodes) {
            if (node.id === id) {
              return node.name;
            }
            if (node.children.length > 0) {
              const found = findNodeName(node.children, id);
              if (found) return found;
            }
          }
          return null;
        };

        const nodeName = findNodeName(treeRef.current, nodeId) || nodeId;

        // If moving to "Unclassified", clear the category by passing empty string
        const categoryValue = nodeId === '__unclassified__' ? '' : nodeId;

        // Update the mod's category using the shared hook
        // Note: modName is optional, pass empty string if not available
        await updateModCategory(modSha, categoryValue, nodeName);
      } catch (error) {
        console.error('[handleModClassify] Error:', error);
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
  };
}

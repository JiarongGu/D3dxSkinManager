import { useState, useCallback } from "react";
import { App } from "antd";
import { ExclamationCircleOutlined } from "@ant-design/icons";
import { Input } from "antd";
import { ClassificationNode } from "../../../../shared/types/classification.types";
import { photinoService } from "../../../../shared/services/photinoService";
import { useProfile } from "../../../../shared/context/ProfileContext";

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
 * (edit, delete, drag & drop)
 */
export function useClassificationTreeOperations({
  tree,
  expandedKeys,
  onExpandedKeysChange,
  onRefreshTree,
}: UseClassificationTreeOperationsProps) {
  const { modal, message } = App.useApp();
  const [draggedNodeId, setDraggedNodeId] = useState<string | null>(null);
  const { selectedProfileId } = useProfile();

  // Edit node handler
  const handleEditNode = useCallback(
    async (nodeId: string) => {
      const node = findNodeById(tree, nodeId);
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
            message.error("Name cannot be empty");
            return Promise.reject();
          }

          try {
            const response = await photinoService.sendMessage<boolean>({
              module: "MOD",
              type: "UPDATE_CLASSIFICATION_NODE",
              profileId: selectedProfileId,
              payload: {
                nodeId,
                name: newName,
              },
            });

            if (response) {
              message.success("Classification updated successfully");
              if (onRefreshTree) {
                await onRefreshTree();
              }
            } else {
              message.error("Failed to update classification");
            }
          } catch (error) {
            console.error("Error updating classification:", error);
            message.error("Failed to update classification");
          }
        },
      });
    },
    [tree, modal, message, onRefreshTree, selectedProfileId],
  );

  // Delete node handler
  const handleDeleteNode = useCallback(
    async (nodeId: string) => {
      const node = findNodeById(tree, nodeId);
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
          try {
            const response = await photinoService.sendMessage<boolean>({
              module: "MOD",
              type: "DELETE_CLASSIFICATION_NODE",
              profileId: selectedProfileId,
              payload: {
                nodeId,
              },
            });

            if (response) {
              message.success("Classification deleted successfully");
              if (onRefreshTree) {
                await onRefreshTree();
              }
            } else {
              message.error("Failed to delete classification");
            }
          } catch (error) {
            console.error("Error deleting classification:", error);
            message.error("Failed to delete classification");
          }
        },
      });
    },
    [tree, modal, message, onRefreshTree, selectedProfileId],
  );

  // Handle drag and drop on tree nodes
  const handleDrop = useCallback(
    async (info: any) => {
      const dropKey = info.node.key;
      const dragKey = info.dragNode.key;
      const dropPos = info.node.pos.split("-");
      const dropPosition =
        info.dropPosition - Number(dropPos[dropPos.length - 1]);

      // Prevent dropping on itself
      if (dragKey === dropKey) {
        return;
      }

      try {
        // Check if dropping into a node or between nodes
        if (info.dropToGap) {
          // Dropping between nodes (reordering or moving to root)
          const dropNode = findNodeById(tree, dropKey);

          // Determine the parent: if dropNode has no parent, we're at root level
          const newParentId = dropNode ? dropNode.parentId || null : null;

          // Calculate the actual position within siblings
          // We need to find siblings and determine where to insert
          let siblings: ClassificationNode[] = [];
          if (newParentId) {
            // Find parent node and get its children
            const parentNode = findNodeById(tree, newParentId);
            siblings = parentNode ? parentNode.children : [];
          } else {
            // Root level siblings
            siblings = tree;
          }

          // Find the index of the drop target node within siblings
          const dropNodeIndex = siblings.findIndex((s) => s.id === dropKey);

          // Calculate final position based on whether dropping before or after
          let finalPosition = dropNodeIndex;
          if (dropPosition > 0) {
            // Dropping after the node
            finalPosition = dropNodeIndex + 1;
          }
          // If dropPosition < 0, we're dropping before, so use dropNodeIndex as-is

          // Send move request to backend
          const response = await photinoService.sendMessage<boolean>({
            module: "MOD",
            type: "MOVE_CLASSIFICATION_NODE",
            profileId: selectedProfileId,
            payload: {
              nodeId: dragKey,
              newParentId: newParentId,
              dropPosition: Math.max(0, finalPosition),
            },
          });

          if (response && onRefreshTree) {
            // Refresh the tree
            await onRefreshTree();
          }
        } else {
          // Dropping into a node (moving to new parent)
          const response = await photinoService.sendMessage<boolean>({
            module: "MOD",
            type: "MOVE_CLASSIFICATION_NODE",
            profileId: selectedProfileId,
            payload: {
              nodeId: dragKey,
              newParentId: dropKey,
              dropPosition: 0,
            },
          });

          if (response && onRefreshTree) {
            // Refresh the tree
            await onRefreshTree();

            // Expand the target node to show the moved item
            if (!expandedKeys.includes(dropKey)) {
              onExpandedKeysChange([...expandedKeys, dropKey]);
            }
          }
        }
      } catch (error) {
        console.error("Error moving classification node:", error);
      }
    },
    [tree, expandedKeys, onExpandedKeysChange, onRefreshTree, selectedProfileId],
  );

  // Track which node is being dragged
  const handleDragStart = useCallback((info: any) => {
    setDraggedNodeId(info.node.key);
  }, []);

  const handleDragEnd = useCallback(() => {
    setDraggedNodeId(null);
  }, []);

  // Handle drop on container (empty space) to make node a root
  const handleContainerDrop = useCallback(
    async (e: React.DragEvent) => {
      // Check if we're dropping on an actual tree node - if so, let Tree handle it
      const target = e.target as HTMLElement;
      if (
        target.closest(".ant-tree-node-content-wrapper") ||
        target.closest(".ant-tree-treenode")
      ) {
        return; // Let the Tree component handle node drops
      }

      e.preventDefault();
      e.stopPropagation();

      if (!draggedNodeId) return;

      try {
        // Move to root level (no parent)
        const response = await photinoService.sendMessage<boolean>({
          module: "MOD",
          type: "MOVE_CLASSIFICATION_NODE",
          profileId: selectedProfileId,
          payload: {
            nodeId: draggedNodeId,
            newParentId: null,
            dropPosition: 0,
          },
        });

        if (response && onRefreshTree) {
          await onRefreshTree();
        }
      } catch (error) {
        console.error("Error moving node to root:", error);
      } finally {
        setDraggedNodeId(null);
      }
    },
    [draggedNodeId, onRefreshTree, selectedProfileId],
  );

  const handleContainerDragOver = useCallback((e: React.DragEvent) => {
    e.preventDefault(); // Allow drop
  }, []);

  return {
    handleEditNode,
    handleDeleteNode,
    handleDrop,
    handleDragStart,
    handleDragEnd,
    handleContainerDrop,
    handleContainerDragOver,
    draggedNodeId,
  };
}

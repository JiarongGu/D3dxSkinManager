import { useState, useCallback } from "react";
import { App } from "antd";
import { ExclamationCircleOutlined } from "@ant-design/icons";
import { Input } from "antd";
import { ClassificationNode } from "../../../../shared/types/classification.types";
import { photinoService } from "../../../../shared/services/photinoService";
import { useProfile } from "../../../../shared/context/ProfileContext";
import { useModCategoryUpdate } from "./useModCategoryUpdate";

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
  onModsRefresh?: () => Promise<void>;
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
  onModsRefresh,
}: UseClassificationTreeOperationsProps) {
  const { modal, message } = App.useApp();
  const [draggedNodeId, setDraggedNodeId] = useState<string | null>(null);
  const { selectedProfileId } = useProfile();
  const { updateModCategory } = useModCategoryUpdate({ onRefreshTree, onModsRefresh });

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
      const dragKey = info.dragNode?.key;

      // Check if this is a mod being dropped (not a tree node)
      // If dragNode is undefined/null, it means something external is being dragged
      if (!dragKey) {
        // This might be a mod drop - check the native event
        const nativeEvent = info.event as DragEvent;
        const modSha = nativeEvent.dataTransfer?.getData('application/mod-sha');

        if (modSha && dropKey) {
          // This is a mod drop!
          await handleModDrop(nativeEvent as any, dropKey);
          return; // Don't proceed with tree node reordering
        }
      }

      // If we get here, it's a tree node being reordered
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

  // Handle external drag from ModList
  const handleModDragOver = useCallback((e: React.DragEvent, nodeId: string) => {
    // Check if this is a mod being dragged (not a classification node)
    const modSha = e.dataTransfer.types.includes('application/mod-sha');
    if (modSha) {
      e.preventDefault();
      e.stopPropagation();
      e.dataTransfer.dropEffect = 'move';
    }
  }, []);

  // Handle mod drop on classification node
  const handleModDrop = useCallback(
    async (e: React.DragEvent, nodeId: string) => {
      e.preventDefault();
      e.stopPropagation();

      const modSha = e.dataTransfer.getData('application/mod-sha');
      const modName = e.dataTransfer.getData('application/mod-name');

      if (!modSha) {
        return;
      }

      // Find the node name from the tree
      const findNodeName = (nodes: ClassificationNode[], id: string): string | null => {
        for (const node of nodes) {
          if (node.id === id) return node.name;
          if (node.children.length > 0) {
            const found = findNodeName(node.children, id);
            if (found) return found;
          }
        }
        return null;
      };

      const nodeName = findNodeName(tree, nodeId) || nodeId;

      // If moving to "Unclassified", clear the category by passing empty string
      const categoryValue = nodeId === '__unclassified__' ? '' : nodeId;

      // Update the mod's category using the shared hook
      await updateModCategory(modSha, modName, categoryValue, nodeName);
    },
    [updateModCategory, tree]
  );

  return {
    handleEditNode,
    handleDeleteNode,
    handleDrop,
    handleDragStart,
    handleDragEnd,
    handleContainerDrop,
    handleContainerDragOver,
    handleModDragOver,
    handleModDrop,
    draggedNodeId,
  };
}

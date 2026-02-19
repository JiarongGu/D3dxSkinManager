import React, { createContext, useContext, useMemo, useState, useCallback } from 'react';
import type { DataNode } from 'antd/es/tree';
import type { MenuProps } from 'antd';
import { ClassificationNode } from '../../../../shared/types/classification.types';
import { convertToDataNode } from './TreeNodeConverter';
import { getClassificationContextMenu } from './ClassificationContextMenu';
import { useClassificationTreeOperations } from './useClassificationTreeOperations';

/**
 * Recursively filter tree nodes by search query
 */
function filterTreeNodes(nodes: ClassificationNode[], searchLower: string): ClassificationNode[] {
  return nodes
    .map(node => {
      // Check if current node matches
      const nodeMatches = node.name.toLowerCase().includes(searchLower);

      // Filter children recursively
      const filteredChildren = filterTreeNodes(node.children, searchLower);

      // Include node if it matches OR has matching children
      if (nodeMatches || filteredChildren.length > 0) {
        return {
          ...node,
          children: filteredChildren,
        };
      }

      return null;
    })
    .filter((node): node is ClassificationNode => node !== null);
}

/**
 * Find a ClassificationNode by ID in the tree
 */
function findNodeById(nodes: ClassificationNode[], id: string): ClassificationNode | null {
  for (const node of nodes) {
    if (node.id === id) return node;
    if (node.children.length > 0) {
      const found = findNodeById(node.children, id);
      if (found) return found;
    }
  }
  return null;
}

/**
 * Props for the ClassificationTreeProvider
 */
interface ClassificationTreeProviderProps {
  children: React.ReactNode;
  tree: ClassificationNode[];
  loading: boolean;
  selectedNode: ClassificationNode | null;
  onSelect: (node: ClassificationNode | null) => void;
  searchQuery: string;
  onSearchChange: (query: string) => void;
  expandedKeys: React.Key[];
  onExpandedKeysChange: (keys: React.Key[]) => void;
  onAddClassification?: (parentId?: string) => void;
  onRefreshTree?: () => Promise<void>;
}

/**
 * Context value type
 */
interface ClassificationTreeContextValue {
  // Props passed from parent
  tree: ClassificationNode[];
  loading: boolean;
  selectedNode: ClassificationNode | null;
  onSelect: (node: ClassificationNode | null) => void;
  searchQuery: string;
  onSearchChange: (query: string) => void;
  expandedKeys: React.Key[];
  onExpandedKeysChange: (keys: React.Key[]) => void;
  onAddClassification?: (parentId?: string) => void;
  onRefreshTree?: () => Promise<void>;

  // Derived state
  filteredTree: ClassificationNode[];
  treeData: DataNode[];

  // Context menu state
  contextMenuNode: string | null;
  setContextMenuNode: (nodeId: string | null) => void;
  contextMenuItems: MenuProps['items'];

  // Operations from hook
  handleEditNode: (nodeId: string) => Promise<void>;
  handleDeleteNode: (nodeId: string) => Promise<void>;
  handleDrop: (info: any) => Promise<void>;
  handleDragStart: (info: any) => void;
  handleDragEnd: () => void;
  handleContainerDrop: (e: React.DragEvent) => Promise<void>;
  handleContainerDragOver: (e: React.DragEvent) => void;

  // Tree handlers
  handleToggleExpand: (nodeId: string) => void;
  handleSelect: (selectedKeys: React.Key[], info: any) => void;
  handleRightClick: (info: { event: any; node: any }) => void;
  findNodeById: (id: string) => ClassificationNode | null;
}

const ClassificationTreeContext = createContext<ClassificationTreeContextValue | undefined>(undefined);

/**
 * Provider component that manages all ClassificationTree state and logic
 */
export const ClassificationTreeProvider: React.FC<ClassificationTreeProviderProps> = ({
  children,
  tree,
  loading,
  selectedNode,
  onSelect,
  searchQuery,
  onSearchChange,
  expandedKeys,
  onExpandedKeysChange,
  onAddClassification,
  onRefreshTree,
}) => {
  const [contextMenuNode, setContextMenuNode] = useState<string | null>(null);

  // Use the operations hook for edit, delete, drag & drop
  const {
    handleEditNode,
    handleDeleteNode,
    handleDrop,
    handleDragStart,
    handleDragEnd,
    handleContainerDrop,
    handleContainerDragOver,
  } = useClassificationTreeOperations({
    tree,
    expandedKeys,
    onExpandedKeysChange,
    onRefreshTree,
  });

  // Get context menu items
  const contextMenuItems = useMemo(
    () =>
      getClassificationContextMenu({
        nodeId: contextMenuNode,
        onAddClassification,
        onEditNode: handleEditNode,
        onDeleteNode: handleDeleteNode,
      }),
    [contextMenuNode, onAddClassification, handleEditNode, handleDeleteNode]
  );

  // Toggle expansion for a folder node - optimized for performance
  const handleToggleExpand = useCallback(
    (nodeId: string) => {
      const isExpanded = expandedKeys.includes(nodeId);

      if (isExpanded) {
        // Collapse: remove this key and all descendant keys
        const node = findNodeById(tree, nodeId);
        if (!node) return;

        const keysToRemove = new Set<React.Key>([nodeId]);

        // Iterative approach instead of recursive for better performance
        const stack = [node];
        while (stack.length > 0) {
          const current = stack.pop()!;
          current.children.forEach((child) => {
            keysToRemove.add(child.id);
            if (child.children.length > 0) {
              stack.push(child);
            }
          });
        }

        onExpandedKeysChange(expandedKeys.filter((k) => !keysToRemove.has(k)));
      } else {
        // Expand: add this key
        onExpandedKeysChange([...expandedKeys, nodeId]);
      }
    },
    [expandedKeys, tree, onExpandedKeysChange]
  );

  // Filter tree based on search query
  const filteredTree = useMemo(() => {
    if (!searchQuery) return tree;
    const searchLower = searchQuery.toLowerCase();
    return filterTreeNodes(tree, searchLower);
  }, [tree, searchQuery]);

  // Convert to Ant Design tree format - direct tree nodes without root wrapper
  const treeData = useMemo((): DataNode[] => {
    return filteredTree.map((node) => convertToDataNode(node, expandedKeys));
  }, [filteredTree, expandedKeys]);

  const handleSelect = useCallback(
    (selectedKeys: React.Key[], info: any) => {
      const key = info.node.key as string;
      const node = findNodeById(tree, key);

      // Check if this is a folder node (has children)
      const isFolderNode = node && node.children.length > 0;

      // Check if we're clicking the already selected node
      const isAlreadySelected = selectedNode?.id === key;

      // For folder nodes: toggle expansion
      if (isFolderNode) {
        requestAnimationFrame(() => {
          handleToggleExpand(key);
        });

        // If clicking an already selected folder, don't trigger selection change
        // (just expand/collapse without reloading mods)
        if (isAlreadySelected) {
          return;
        }
      }

      // Handle selection (only if not already selected or if it's a leaf node)
      if (selectedKeys.length === 0) {
        onSelect(null);
        return;
      }

      onSelect(node);
    },
    [tree, handleToggleExpand, onSelect, selectedNode]
  );

  const handleRightClick = useCallback(({ event, node }: any) => {
    event.preventDefault();
    setContextMenuNode(node.key as string);
  }, []);

  const findNode = useCallback(
    (id: string) => {
      return findNodeById(tree, id);
    },
    [tree]
  );

  const contextValue: ClassificationTreeContextValue = {
    // Props
    tree,
    loading,
    selectedNode,
    onSelect,
    searchQuery,
    onSearchChange,
    expandedKeys,
    onExpandedKeysChange,
    onAddClassification,
    onRefreshTree,

    // Derived state
    filteredTree,
    treeData,

    // Context menu
    contextMenuNode,
    setContextMenuNode,
    contextMenuItems,

    // Operations
    handleEditNode,
    handleDeleteNode,
    handleDrop,
    handleDragStart,
    handleDragEnd,
    handleContainerDrop,
    handleContainerDragOver,

    // Tree handlers
    handleToggleExpand,
    handleSelect,
    handleRightClick,
    findNodeById: findNode,
  };

  return (
    <ClassificationTreeContext.Provider value={contextValue}>
      {children}
    </ClassificationTreeContext.Provider>
  );
};

/**
 * Hook to access the ClassificationTree context
 */
export const useClassificationTreeContext = () => {
  const context = useContext(ClassificationTreeContext);
  if (!context) {
    throw new Error(
      'useClassificationTreeContext must be used within a ClassificationTreeProvider'
    );
  }
  return context;
};

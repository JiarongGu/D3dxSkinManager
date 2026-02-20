import React, { createContext, useContext, useMemo, useState, useCallback } from 'react';
import type { DataNode } from 'antd/es/tree';
import type { MenuProps } from 'antd';
import { ClassificationNode } from '../../../../shared/types/classification.types';
import { convertToDataNode } from './TreeNodeConverter';
import { getClassificationContextMenu } from './ClassificationContextMenu';
import { useClassificationTreeOperations } from './useClassificationTreeOperations';
import { useStableRef } from '../../../../shared/hooks/useStableRef';

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
  onModsRefresh?: () => Promise<void>;
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
  contextMenuPosition: { x: number; y: number };
  setContextMenuPosition: (position: { x: number; y: number }) => void;

  // Operations from hook
  handleEditNode: (nodeId: string) => Promise<void>;
  handleDeleteNode: (nodeId: string) => Promise<void>;
  handleNodeReorder: (
    dragNodeId: string,
    dropNodeId: string,
    dropType: 'node' | 'gap',
    gapSide?: 'top' | 'bottom'
  ) => Promise<void>;
  handleModClassify: (modSha: string, nodeId: string) => Promise<void>;

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
  onModsRefresh,
}) => {
  const [contextMenuNode, setContextMenuNode] = useState<string | null>(null);
  const [contextMenuPosition, setContextMenuPosition] = useState({ x: 0, y: 0 });

  // Store frequently changing values in stable refs to avoid closure issues
  const [treeRef, expandedKeysRef, selectedNodeRef] = useStableRef(tree, expandedKeys, selectedNode);

  // Use the operations hook for edit, delete, drag & drop
  const {
    handleEditNode,
    handleDeleteNode,
    handleNodeReorder,
    handleModClassify,
  } = useClassificationTreeOperations({
    tree,
    expandedKeys,
    onExpandedKeysChange,
    onRefreshTree, // ✅ Pass refresh callback
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
      const currentExpandedKeys = expandedKeysRef.current;
      const currentTree = treeRef.current;
      const isExpanded = currentExpandedKeys.includes(nodeId);

      if (isExpanded) {
        // Collapse: remove this key and all descendant keys
        const node = findNodeById(currentTree, nodeId);
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

        onExpandedKeysChange(currentExpandedKeys.filter((k) => !keysToRemove.has(k)));
      } else {
        // Expand: add this key
        onExpandedKeysChange([...currentExpandedKeys, nodeId]);
      }
    },
    [onExpandedKeysChange] // treeRef and expandedKeysRef are stable refs
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
      const currentTree = treeRef.current;
      const currentSelectedNode = selectedNodeRef.current;
      const key = info.node.key as string;
      const node = findNodeById(currentTree, key);

      // Check if this is a folder node (has children)
      const isFolderNode = node && node.children.length > 0;

      // Check if we're clicking the already selected node
      const isAlreadySelected = currentSelectedNode?.id === key;

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
    [handleToggleExpand, onSelect] // treeRef and selectedNodeRef are stable refs
  );

  const handleRightClick = useCallback(({ event, node }: any) => {
    event.preventDefault();
    const newNodeKey = node.key as string;

    // Capture mouse position
    setContextMenuPosition({ x: event.clientX, y: event.clientY });
    setContextMenuNode(newNodeKey);
  }, []);

  const findNode = useCallback(
    (id: string) => {
      return findNodeById(treeRef.current, id);
    },
    [] // treeRef is a stable ref
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

    // Derived state
    filteredTree,
    treeData,

    // Context menu
    contextMenuNode,
    setContextMenuNode,
    contextMenuItems,
    contextMenuPosition,
    setContextMenuPosition,

    // Operations
    handleEditNode,
    handleDeleteNode,
    handleNodeReorder,
    handleModClassify,

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

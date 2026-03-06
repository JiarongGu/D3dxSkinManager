import React, { createContext, useContext, useMemo, useState, useCallback } from 'react';
import type { DataNode } from 'antd/es/tree';
import type { MenuProps } from 'antd';
import { ExclamationCircleOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { debounce } from 'lodash-es';
import { CategoryInfo } from '../../../../shared/types/category.types';
import { convertToDataNode } from './TreeNodeConverter';
import { getCategoryContextMenu } from './CategoryContextMenu';
import { useCategoryTreeOperations } from './useCategoryTreeOperations';
import { useStableRef } from '../../../../shared/hooks/useStableRef';
import { ConfirmDialog } from '../../../../shared/components/dialogs/ConfirmDialog';
import { useModsStore } from '../../store/modsStore';
import { profileService } from '../../../../shared/services/ipc';
import { useProfile } from '../../../../shared/context/ProfileContext';
import './CategoryTreeContext.css';

/**
 * Recursively filter tree nodes by search query
 */
function filterTreeNodes(nodes: CategoryInfo[], searchLower: string): CategoryInfo[] {
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
    .filter((node): node is CategoryInfo => node !== null);
}

/**
 * Find a CategoryInfo by ID in the tree
 */
function findNodeById(nodes: CategoryInfo[], id: string): CategoryInfo | undefined {
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
 * Props for the CategoryTreeProvider
 */
interface CategoryTreeProviderProps {
  children: React.ReactNode;
  tree: CategoryInfo[];
  loading: boolean;
  selectedNode: CategoryInfo | undefined;
  onSelect: (node: CategoryInfo | undefined) => void;
  searchQuery: string;
  onSearchChange: (query: string) => void;
  expandedKeys: React.Key[];
  onExpandedKeysChange: (keys: React.Key[]) => void;
  onAddCategory?: (parentId?: string) => void;
  onModsRefresh?: () => Promise<void>;
}

/**
 * Context value type
 */
interface CategoryTreeContextValue {
  // Props passed from parent
  tree: CategoryInfo[];
  loading: boolean;
  selectedNode: CategoryInfo | undefined;
  onSelect: (node: CategoryInfo | undefined) => void;
  searchQuery: string;
  onSearchChange: (query: string) => void;
  expandedKeys: React.Key[];
  onExpandedKeysChange: (keys: React.Key[]) => void;
  onAddCategory?: (parentId?: string) => void;

  // Derived state
  filteredTree: CategoryInfo[];
  treeData: DataNode[];

  // Context menu state
  contextMenuNode: string | undefined;
  setContextMenuNode: (nodeId: string | undefined) => void;
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
  handleBulkModClassify: (modShas: string[], nodeId: string) => Promise<void>;

  // Tree handlers
  handleToggleExpand: (nodeId: string) => void;
  handleSelect: (selectedKeys: React.Key[], info: any) => void;
  handleRightClick: (info: { event: any; node: any }) => void;
  findNodeById: (id: string) => CategoryInfo | undefined;
}

const CategoryTreeContext = createContext<CategoryTreeContextValue | undefined>(undefined);

/**
 * Provider component that manages all CategoryTree state and logic
 */
export const CategoryTreeProvider: React.FC<CategoryTreeProviderProps> = ({
  children,
  tree,
  loading,
  selectedNode,
  onSelect,
  searchQuery,
  onSearchChange,
  expandedKeys,
  onExpandedKeysChange,
  onAddCategory,
  onModsRefresh,
}) => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();
  const [contextMenuNode, setContextMenuNode] = useState<string>();
  const [contextMenuPosition, setContextMenuPosition] = useState({ x: 0, y: 0 });

  // Use locked categories from store (persisted across tab switches)
  const lockedCategories = useModsStore(s => s.lockedCategories);
  const addLockedCategory = useModsStore(s => s.addLockedCategory);
  const removeLockedCategory = useModsStore(s => s.removeLockedCategory);
  const lockedCategoriesSet = useMemo(() => new Set(lockedCategories), [lockedCategories]);

  // Store frequently changing values in stable refs to avoid closure issues
  const [treeRef, expandedKeysRef, selectedNodeRef] = useStableRef(tree, expandedKeys, selectedNode);

  // Debounced save to backend (must be declared before useEffect)
  const debouncedSaveLockedCategories = useMemo(
    () => debounce(async (lockedKeys: string[], profileId: string | undefined) => {
      if (!profileId) return;
      try {
        await profileService.updateLockedExpandedCategories(profileId, lockedKeys);
      } catch (error) {
        console.error('[CategoryTreeContext] Failed to save locked expanded categories:', error);
      }
    }, 200),
    []
  );

  // Validate locked categories when tree changes
  React.useEffect(() => {
    // Build a map of all category IDs and check if they're parent nodes
    const categoryMap = new Map<string, boolean>(); // id -> isParent

    const buildCategoryMap = (nodes: CategoryInfo[]) => {
      for (const node of nodes) {
        categoryMap.set(node.id, node.children.length > 0);
        if (node.children.length > 0) {
          buildCategoryMap(node.children);
        }
      }
    };

    buildCategoryMap(tree);

    // Check if any locked categories are now invalid
    const invalidLockedKeys: string[] = [];
    for (const lockedKey of lockedCategories) {
      const isParent = categoryMap.get(lockedKey);
      // Invalid if: doesn't exist OR is no longer a parent (became a leaf node)
      if (isParent === undefined || isParent === false) {
        invalidLockedKeys.push(lockedKey);
      }
    }

    // Remove invalid locked keys
    if (invalidLockedKeys.length > 0) {
      console.log('[CategoryTreeContext] Removing invalid locked categories:', invalidLockedKeys);
      const newLockedKeys = lockedCategories.filter(k => !invalidLockedKeys.includes(k));
      useModsStore.getState().setLockedCategories(newLockedKeys);
      // Persist to backend
      if (selectedProfileId) {
        debouncedSaveLockedCategories(newLockedKeys, selectedProfileId);
      }
    }
  }, [tree, lockedCategories, selectedProfileId, debouncedSaveLockedCategories]);

  // Lock/unlock expansion handlers
  const handleLockExpanded = useCallback((nodeId: string) => {
    addLockedCategory(nodeId);
    // Ensure the node is expanded when locked
    if (!expandedKeys.includes(nodeId)) {
      onExpandedKeysChange([...expandedKeys, nodeId]);
    }
    // Persist to backend
    debouncedSaveLockedCategories([...lockedCategories, nodeId], selectedProfileId);
  }, [addLockedCategory, expandedKeys, onExpandedKeysChange, lockedCategories, selectedProfileId, debouncedSaveLockedCategories]);

  const handleUnlockExpanded = useCallback((nodeId: string) => {
    removeLockedCategory(nodeId);
    // Persist to backend
    const newLockedKeys = lockedCategories.filter(k => k !== nodeId);
    debouncedSaveLockedCategories(newLockedKeys, selectedProfileId);
  }, [removeLockedCategory, lockedCategories, selectedProfileId, debouncedSaveLockedCategories]);

  // Use the operations hook for edit, delete, drag & drop
  const {
    handleEditNode,
    handleDeleteNode,
    handleNodeReorder,
    handleModClassify,
    handleBulkModClassify,
    deleteConfirmation,
    handleDeleteConfirm,
    closeDeleteConfirmation,
  } = useCategoryTreeOperations({
    tree,
    expandedKeys,
    selectedCategoryId: selectedNode?.id,
    onExpandedKeysChange,
    onModsRefresh, // ✅ Pass mods refresh callback
  });

  // Get context menu items
  const contextMenuItems = useMemo(() => {
    return getCategoryContextMenu({
      nodeId: contextMenuNode,
      onAddCategory,
      onEditNode: handleEditNode,
      onDeleteNode: handleDeleteNode,
      t,
    });
  }, [contextMenuNode, onAddCategory, handleEditNode, handleDeleteNode, t]);

  // Toggle expansion for a folder node - optimized for performance
  const handleToggleExpand = useCallback(
    (nodeId: string) => {
      const currentExpandedKeys = expandedKeysRef.current;
      const currentTree = treeRef.current;
      const isExpanded = currentExpandedKeys.includes(nodeId);

      if (isExpanded) {
        // Check if node is locked - prevent collapse if locked
        if (lockedCategoriesSet.has(nodeId)) {
          return; // Don't collapse locked nodes
        }

        // Collapse: remove this key and all descendant keys (except locked ones)
        const node = findNodeById(currentTree, nodeId);
        if (!node) return;

        const keysToRemove = new Set<React.Key>([nodeId]);

        // Iterative approach instead of recursive for better performance
        const stack = [node];
        while (stack.length > 0) {
          const current = stack.pop()!;
          current.children.forEach((child) => {
            // Don't remove locked keys
            if (!lockedCategoriesSet.has(child.id)) {
              keysToRemove.add(child.id);
            }
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
    [lockedCategoriesSet, onExpandedKeysChange] // treeRef and expandedKeysRef are stable refs
  );

  // Filter tree based on search query
  const filteredTree = useMemo(() => {
    if (!searchQuery) return tree;
    const searchLower = searchQuery.toLowerCase();
    return filterTreeNodes(tree, searchLower);
  }, [tree, searchQuery]);

  // Handler for clicking lock icon to unlock
  const handleLockIconClick = useCallback((nodeId: string, e: React.MouseEvent) => {
    e.stopPropagation();
    handleUnlockExpanded(nodeId);
  }, [handleUnlockExpanded]);

  // Handler for clicking unlock icon to lock
  const handleUnlockIconClick = useCallback((nodeId: string, e: React.MouseEvent) => {
    e.stopPropagation();
    handleLockExpanded(nodeId);
  }, [handleLockExpanded]);

  // Convert to Ant Design tree format - direct tree nodes without root wrapper
  const treeData = useMemo((): DataNode[] => {
    return filteredTree.map((node) => convertToDataNode(node, expandedKeys, lockedCategoriesSet, handleLockIconClick, handleUnlockIconClick));
  }, [filteredTree, expandedKeys, lockedCategoriesSet, handleLockIconClick, handleUnlockIconClick]);

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

      // For folder nodes: toggle expansion (unless locked)
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

      // Handle selection
      if (selectedKeys.length === 0) {
        onSelect(undefined);
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

  const contextValue: CategoryTreeContextValue = {
    // Props
    tree,
    loading,
    selectedNode,
    onSelect,
    searchQuery,
    onSearchChange,
    expandedKeys,
    onExpandedKeysChange,
    onAddCategory,

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
    handleBulkModClassify,

    // Tree handlers
    handleToggleExpand,
    handleSelect,
    handleRightClick,
    findNodeById: findNode,
  };

  return (
    <CategoryTreeContext.Provider value={contextValue}>
      {children}
      <ConfirmDialog
        visible={deleteConfirmation.visible}
        title={t('category.delete.title')}
        content={deleteConfirmation.hasChildren
          ? t('category.delete.withChildrenMessage', { name: deleteConfirmation.nodeName })
          : t('category.delete.message', { name: deleteConfirmation.nodeName })
        }
        okText={t('common.delete')}
        cancelText={t('common.cancel')}
        okType="danger"
        icon={<ExclamationCircleOutlined className="category-tree-delete-icon" />}
        onOk={handleDeleteConfirm}
        onCancel={closeDeleteConfirmation}
      />
    </CategoryTreeContext.Provider>
  );
};

/**
 * Hook to access the CategoryTree context
 */
export const useCategoryTreeContext = () => {
  const context = useContext(CategoryTreeContext);
  if (!context) {
    throw new Error(
      'useCategoryTreeContext must be used within a CategoryTreeProvider'
    );
  }
  return context;
};

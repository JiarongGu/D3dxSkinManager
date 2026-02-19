import React from 'react';
import { Tree, Empty, Spin, Input, Button, Tooltip } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import { ClassificationNode } from '../../../../shared/types/classification.types';
import { ClassificationTreeProvider, useClassificationTreeContext } from './ClassificationTreeContext';
import { ContextMenu, ContextMenuItem } from '../../../../shared/components/menu/ContextMenu';
import './ClassificationTree.css';

const { Search } = Input;

/**
 * Props for the main ClassificationTree component
 */
export interface ClassificationTreeProps {
  /**
   * Classification tree data from backend
   */
  tree: ClassificationNode[];

  /**
   * Loading state
   */
  loading: boolean;

  /**
   * Selected classification node
   */
  selectedNode: ClassificationNode | null;

  /**
   * Callback when a node is selected
   */
  onSelect: (node: ClassificationNode | null) => void;

  /**
   * Search query for filtering tree
   */
  searchQuery: string;

  /**
   * Callback when search query changes
   */
  onSearchChange: (query: string) => void;

  /**
   * Expanded keys for tree
   */
  expandedKeys: React.Key[];

  /**
   * Callback when expanded keys change
   */
  onExpandedKeysChange: (keys: React.Key[]) => void;

  /**
   * Callback when creating new classification
   */
  onAddClassification?: (parentId?: string) => void;

  /**
   * Callback to refresh tree after operations
   */
  onRefreshTree?: () => Promise<void>;

  /**
   * Callback to refresh mods after category update
   */
  onModsRefresh?: () => Promise<void>;
}

/**
 * Internal tree component that uses the context
 */
const ClassificationTreeInner: React.FC = () => {
  const {
    loading,
    tree,
    selectedNode,
    searchQuery,
    onSearchChange,
    onAddClassification,
    contextMenuNode,
    setContextMenuNode,
    contextMenuItems,
    contextMenuPosition,
    setContextMenuPosition,
    treeData,
    expandedKeys,
    handleSelect,
    handleRightClick,
    handleDrop,
    handleDragStart,
    handleDragEnd,
    handleContainerDrop,
    handleContainerDragOver,
    handleModDragOver,
    handleModDrop,
  } = useClassificationTreeContext();

  const treeContainerRef = React.useRef<HTMLDivElement>(null);

  // Add DOM-level event listeners to capture external drops on tree nodes
  React.useEffect(() => {
    if (!treeContainerRef.current) return;

    const container = treeContainerRef.current;

    const handleDragOver = (e: DragEvent) => {
      // Check if mod is being dragged
      const types = Array.from(e.dataTransfer?.types || []);

      if (types.includes('application/mod-sha')) {
        e.preventDefault(); // Allow drop
        e.dataTransfer!.dropEffect = 'move';

        // Add visual feedback
        const target = e.target as HTMLElement;
        const nodeWrapper = target.closest('.ant-tree-node-content-wrapper');

        if (nodeWrapper) {
          nodeWrapper.classList.add('mod-drop-target');
        }
      }
    };

    const handleDragLeave = (e: DragEvent) => {
      const target = e.target as HTMLElement;
      const nodeWrapper = target.closest('.ant-tree-node-content-wrapper');
      if (nodeWrapper) {
        nodeWrapper.classList.remove('mod-drop-target');
      }
    };

    const handleDrop = (e: DragEvent) => {
      // Remove all visual feedback
      container.querySelectorAll('.mod-drop-target').forEach(el => {
        el.classList.remove('mod-drop-target');
      });

      const modSha = e.dataTransfer?.getData('application/mod-sha');

      if (modSha) {
        e.preventDefault();
        e.stopPropagation();

        // Find which node was dropped on
        const target = e.target as HTMLElement;
        const nodeWrapper = target.closest('.ant-tree-node-content-wrapper');
        const treeNode = nodeWrapper?.closest('.ant-tree-treenode');

        if (treeNode) {
          // Try multiple methods to get the node key
          let nodeKey: string | null = null;

          // Method 1: Try React fiber
          const fiberKey = Object.keys(treeNode).find(key => key.startsWith('__reactFiber'));
          if (fiberKey) {
            const fiber = (treeNode as any)[fiberKey];
            nodeKey = fiber?.key || fiber?.memoizedProps?.eventKey || fiber?.memoizedProps?.data?.key;
          }

          // Method 2: Try title element's data
          if (!nodeKey && nodeWrapper) {
            const titleElement = nodeWrapper.querySelector('[data-node-id]');
            if (titleElement) {
              nodeKey = titleElement.getAttribute('data-node-id');
            }
          }

          // Method 3: Get from the title text and match against tree data
          if (!nodeKey && nodeWrapper) {
            const titleText = nodeWrapper.textContent?.trim();
            if (titleText) {
              // Strip the count suffix like " (5)"
              const cleanTitle = titleText.replace(/\s*\(\d+\)$/, '');
              nodeKey = cleanTitle; // Use the title as the key (classification node IDs match their names)
            }
          }

          if (nodeKey) {
            handleModDrop(e as any, nodeKey);
          }
        }
      }
    };

    container.addEventListener('dragover', handleDragOver, true);
    container.addEventListener('dragleave', handleDragLeave, true);
    container.addEventListener('drop', handleDrop, true);

    return () => {
      container.removeEventListener('dragover', handleDragOver, true);
      container.removeEventListener('dragleave', handleDragLeave, true);
      container.removeEventListener('drop', handleDrop, true);
    };
  }, [handleModDrop, treeData]); // Re-run when treeData changes (ensures ref is set)

  // Convert Ant Design menu items to ContextMenuItem format
  const rightClickMenuItems: ContextMenuItem[] = (contextMenuItems || []).map((item: any) => ({
    key: item.key,
    label: item.label,
    icon: item.icon,
    danger: item.danger,
    disabled: item.disabled,
    onClick: item.onClick,
    type: item.type,
  }));

  if (loading) {
    return (
      <div style={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        height: '100%',
        padding: '24px'
      }}>
        <Spin description="Loading classification tree..." />
      </div>
    );
  }

  if (tree.length === 0) {
    return (
      <>
        <div
          style={{
            padding: '16px',
            userSelect: 'none',
            height: '100%',
            minHeight: '300px',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            paddingBottom: '80px',
          }}
          onContextMenu={(e) => {
            e.preventDefault();
            setContextMenuPosition({ x: e.clientX, y: e.clientY });
            setContextMenuNode(null); // null for empty tree
          }}
        >
          <Empty
            description="No classification data available. Right-click to add one."
            image={Empty.PRESENTED_IMAGE_SIMPLE}
          />
        </div>
        <ContextMenu
          items={rightClickMenuItems}
          visible={contextMenuNode !== null}
          position={contextMenuPosition}
          onClose={() => setContextMenuNode(null)}
        />
      </>
    );
  }

  return (
    <div style={{ userSelect: 'none', flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column' }}>
      <div style={{ display: 'flex', gap: '8px', padding: '8px 8px 8px 8px', flexShrink: 0 }}>
        <Search
          placeholder="Search classifications..."
          value={searchQuery}
          onChange={(e) => onSearchChange(e.target.value)}
          style={{ flex: 1 }}
          allowClear
        />
        <Tooltip title="Add Classification" placement="top">
          <Button
            type="default"
            icon={<PlusOutlined />}
            onClick={() => {
              if (onAddClassification) {
                onAddClassification();
              }
            }}
          />
        </Tooltip>
      </div>

      <div
        ref={treeContainerRef}
        style={{ flex: 1, minHeight: 0, overflow: 'auto', paddingRight: '4px' }}
        onDrop={handleContainerDrop}
        onDragOver={handleContainerDragOver}
      >
        <div
          style={{ paddingLeft: '8px', paddingBottom: '8px' }}
          onContextMenu={(e) => {
            // Handle context menu on empty areas (not on tree nodes)
            const target = e.target as HTMLElement;
            // Check if click is on tree node or empty area
            if (!target.closest('.ant-tree-node-content-wrapper')) {
              e.preventDefault();
              e.stopPropagation();
              setContextMenuPosition({ x: e.clientX, y: e.clientY });
              setContextMenuNode(''); // Empty string for empty area context menu
            }
          }}
        >
          <Tree
            className="classification-tree"
            showIcon
            draggable
            selectedKeys={selectedNode ? [selectedNode.id] : []}
            expandedKeys={expandedKeys}
            onSelect={handleSelect}
            onRightClick={handleRightClick}
            onDrop={handleDrop}
            onDragStart={handleDragStart}
            onDragEnd={handleDragEnd}
            treeData={treeData}
            style={{ background: 'transparent' }}
          />
        </div>
        <ContextMenu
          items={rightClickMenuItems}
          visible={contextMenuNode !== null}
          position={contextMenuPosition}
          onClose={() => setContextMenuNode(null)}
        />
      </div>
    </div>
  );
};

/**
 * Classification tree component displaying hierarchical character categories.
 *
 * This component uses a context provider to manage all state and logic internally.
 * It provides a clean API for parent components while keeping internal complexity isolated.
 */
export const ClassificationTree: React.FC<ClassificationTreeProps> = (props) => {
  return (
    <ClassificationTreeProvider {...props}>
      <ClassificationTreeInner />
    </ClassificationTreeProvider>
  );
};

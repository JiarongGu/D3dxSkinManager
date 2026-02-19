import React from 'react';
import { Tree, Empty, Spin, Input, Dropdown, Button, Tooltip } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import { ClassificationNode } from '../../../../shared/types/classification.types';
import { ClassificationTreeProvider, useClassificationTreeContext } from './ClassificationTreeContext';
import { getClassificationContextMenu } from './ClassificationContextMenu';
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
    treeData,
    expandedKeys,
    handleSelect,
    handleRightClick,
    handleDrop,
    handleDragStart,
    handleDragEnd,
    handleContainerDrop,
    handleContainerDragOver,
    handleEditNode,
    handleDeleteNode,
  } = useClassificationTreeContext();

  if (loading) {
    return (
      <div style={{ padding: '24px', textAlign: 'center' }}>
        <Spin description="Loading classification tree..." />
      </div>
    );
  }

  if (tree.length === 0) {
    // Allow right-click to add root classification even when empty
    const emptyContextMenu = getClassificationContextMenu({
      nodeId: null,
      onAddClassification,
      onEditNode: handleEditNode,
      onDeleteNode: handleDeleteNode,
    });

    return (
      <Dropdown menu={{ items: emptyContextMenu }} trigger={['contextMenu']}>
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
        >
          <Empty
            description="No classification data available. Right-click to add one."
            image={Empty.PRESENTED_IMAGE_SIMPLE}
          />
        </div>
      </Dropdown>
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
        style={{ flex: 1, minHeight: 0, overflow: 'auto', paddingRight: '4px' }}
        onDrop={handleContainerDrop}
        onDragOver={handleContainerDragOver}
      >
        <Dropdown
          menu={{ items: contextMenuItems }}
          trigger={['contextMenu']}
          open={contextMenuNode !== null}
          onOpenChange={(open) => {
            if (!open) setContextMenuNode(null);
          }}
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
        </Dropdown>
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

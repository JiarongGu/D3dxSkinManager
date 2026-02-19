import React from 'react';
import { Layout, message } from 'antd';
import type { Key } from 'react';
import { ClassificationNode } from '../../../../shared/types/classification.types';
import { ClassificationTree } from './ClassificationTree';
import { UnclassifiedItem } from './UnclassifiedItem';
import { useClassificationScreen } from './ClassificationScreen';

const { Sider } = Layout;

interface ClassificationPanelProps {
  tree: ClassificationNode[];
  loading: boolean;
  selectedNode: ClassificationNode | null;
  onSelect: (node: ClassificationNode | null) => void;
  searchQuery: string;
  onSearchChange: (query: string) => void;
  expandedKeys: Key[];
  onExpandedKeysChange: (keys: Key[]) => void;
  onRefreshTree: () => Promise<void>;
  unclassifiedCount: number;
  onUnclassifiedClick: () => void;
  isUnclassifiedSelected: boolean;
}

export const ClassificationPanel: React.FC<ClassificationPanelProps> = ({
  tree,
  loading,
  selectedNode,
  onSelect,
  searchQuery,
  onSearchChange,
  expandedKeys,
  onExpandedKeysChange,
  onRefreshTree,
  unclassifiedCount,
  onUnclassifiedClick,
  isUnclassifiedSelected,
}) => {
  const { openClassificationScreen } = useClassificationScreen();

  const handleAddClassification = (parentId?: string) => {
    openClassificationScreen({
      parentId,
      tree,
      onSave: async (data) => {
        console.log('Save classification:', data);
        // TODO: Call backend API to create classification
        message.success(`Classification "${data.name}" created successfully`);
      },
    });
  };

  return (
    <Sider
      width={250}
      style={{
        borderRight: '1px solid var(--color-border-secondary)',
        height: '100%',
        overflow: 'hidden',
        display: 'flex',
        flexDirection: 'column',
      }}
    >
      {/* Tree container with flex constraint to allow scrolling */}
      <div
        style={{
          flex: 1,
          minHeight: 0,
          height: 'calc(100% - 40px)',
          display: 'flex',
          flexDirection: 'column',
        }}
      >
        <ClassificationTree
          tree={tree}
          loading={loading}
          selectedNode={selectedNode}
          onSelect={onSelect}
          searchQuery={searchQuery}
          onSearchChange={onSearchChange}
          expandedKeys={expandedKeys}
          onExpandedKeysChange={onExpandedKeysChange}
          onRefreshTree={onRefreshTree}
          onAddClassification={handleAddClassification}
        />
      </div>

      {/* Unclassified Item - fixed at bottom, doesn't scroll */}
      <div
        style={{
          borderTop: '1px solid var(--color-border-secondary)',
          height: '40px',
        }}
      >
        <UnclassifiedItem
          count={unclassifiedCount}
          isSelected={isUnclassifiedSelected}
          onClick={onUnclassifiedClick}
        />
      </div>
    </Sider>
  );
};

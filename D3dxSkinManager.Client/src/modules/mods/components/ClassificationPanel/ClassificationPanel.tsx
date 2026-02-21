import { notification } from '../../../../shared/utils/notification';
import React, { useCallback } from 'react';
import { Layout } from 'antd';
import type { Key } from 'react';
import { ClassificationNode } from '../../../../shared/types/classification.types';
import { ClassificationTree } from './ClassificationTree';
import { UnclassifiedItem } from './UnclassifiedItem';
import { useClassificationScreen } from './ClassificationScreen';
import { useModCategoryUpdate } from './useModCategoryUpdate';
import { useProfile } from '../../../../shared/context/ProfileContext';
import { classificationService } from '../../../../shared/services/classificationService';
import { useTranslation } from 'react-i18next';
import { useDelayedLoading } from '../../../../shared/hooks/useDelayedLoading';

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
  onModsRefresh?: () => Promise<void>;
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
  onModsRefresh,
  unclassifiedCount,
  onUnclassifiedClick,
  isUnclassifiedSelected,
}) => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();
  const { openClassificationScreen } = useClassificationScreen();
  const { updateModCategory } = useModCategoryUpdate({ onRefreshTree });
  const { loading: delayedLoading, execute } = useDelayedLoading(100); // Show loading only if operation takes >100ms

  const handleAddClassification = (parentId?: string) => {
    openClassificationScreen({
      parentId,
      tree,
      onSave: async (data) => {
        if (!selectedProfileId) {
          notification.error(t('common.errors.noProfileSelected'));
          return;
        }

        // Wrap operation in delayed loading execution
        await execute(async () => {
          try {
            // Create the classification node via backend
            const createdNode = await classificationService.createNode(
              selectedProfileId,
              data.name,
              data.name,
              data.parentId || null,
              100, // default priority
              data.description,
              data.thumbnail
            );

            if (createdNode) {
              notification.success(t('classification.createSuccess', { name: data.name }));
              // Refresh the classification tree to show the new node
              await onRefreshTree();
            } else {
              notification.error(t('classification.createFailed', { name: data.name }));
            }
          } catch (error: unknown) {
            const errorMessage = error instanceof Error ? error.message : 'Unknown error';
            notification.error(t('classification.createError', { error: errorMessage }));
          }
        });
      },
    });
  };

  // Handle dropping mods on Unclassified item
  const handleUnclassifiedDrop = useCallback(async (sha?: string) => {
    if (!sha) {
      return;
    }
    // Pass empty string for modName since we don't have it here
    // The updateModCategory function uses it only for the success message
    await updateModCategory(sha, '', 'Unclassified');
    if (onModsRefresh) {
      await onModsRefresh();
    }
  }, [updateModCategory, onModsRefresh]);

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
          loading={loading || delayedLoading}
          selectedNode={selectedNode}
          onSelect={onSelect}
          searchQuery={searchQuery}
          onSearchChange={onSearchChange}
          expandedKeys={expandedKeys}
          onExpandedKeysChange={onExpandedKeysChange}
          onRefreshTree={onRefreshTree}
          onModsRefresh={onModsRefresh}
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
          onModDrop={handleUnclassifiedDrop}
        />
      </div>
    </Sider>
  );
};

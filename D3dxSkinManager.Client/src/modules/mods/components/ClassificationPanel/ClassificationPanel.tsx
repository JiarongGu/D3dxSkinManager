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
import { useModsStore } from '../../store/modsStore';
import { useMods } from '../../hooks/useMods';
import { classificationService } from '../../../../shared/services/classificationService';
import { useTranslation } from 'react-i18next';
import { useDelayedLoading } from '../../../../shared/hooks/useDelayedLoading';
import './ClassificationPanel.css';

const { Sider } = Layout;

interface ClassificationPanelProps {
  onSelect: (node: ClassificationNode | undefined) => void; // Coordination callback with load logic
  onRefreshTree: () => Promise<void>;
  onModsRefresh?: () => Promise<void>;
  unclassifiedCount: number;
  onUnclassifiedClick: () => void;
}

/**
 * ClassificationPanel
 *
 * NEW ARCHITECTURE:
 * - Subscribes to its own state from useModsStore
 * - Receives coordination callbacks from parent (onSelect includes load logic)
 * - Much cleaner - reduced from 13 props to 5!
 */
export const ClassificationPanel: React.FC<ClassificationPanelProps> = ({
  onSelect,
  onRefreshTree,
  onModsRefresh,
  unclassifiedCount,
  onUnclassifiedClick,
}) => {
  // Subscribe to state this component needs
  const tree = useModsStore(s => s.classificationTree);
  const loading = useModsStore(s => s.classificationLoading);
  const selectedNode = useModsStore(s => s.selectedClassification);
  const searchQuery = useModsStore(s => s.classificationSearch);
  const expandedKeys = useModsStore(s => s.expandedKeys);

  // Get operations
  const { setClassificationSearch, setExpandedKeys } = useMods();

  // Is unclassified selected?
  const isUnclassifiedSelected = selectedNode?.id === "__unclassified__";
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
              data.parentId,
              100, // default priority
              data.description,
              data.thumbnail,
              data.matchMode,
              data.matchPattern
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
      className="classification-panel-sider"
    >
      {/* Tree container with flex constraint to allow scrolling */}
      <div className="classification-panel-tree-container">
        <ClassificationTree
          tree={tree}
          loading={loading || delayedLoading}
          selectedNode={selectedNode}
          onSelect={onSelect}
          searchQuery={searchQuery}
          onSearchChange={setClassificationSearch}
          expandedKeys={expandedKeys}
          onExpandedKeysChange={setExpandedKeys}
          onRefreshTree={onRefreshTree}
          onModsRefresh={onModsRefresh}
          onAddClassification={handleAddClassification}
        />
      </div>

      {/* Unclassified Item - fixed at bottom, doesn't scroll */}
      <div className="classification-panel-unclassified-container">
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

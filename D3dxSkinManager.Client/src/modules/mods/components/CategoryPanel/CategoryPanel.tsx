import { notification } from '../../../../shared/utils/notification';
import React, { useCallback } from 'react';
import { Layout } from 'antd';
import type { Key } from 'react';
import { CategoryInfo } from '../../../../shared/types/category.types';
import { CategoryTree } from './CategoryTree';
import { UnclassifiedItem } from './UnclassifiedItem';
import { useCategoryScreen } from './CategoryScreen';
import { useModCategoryUpdate } from './useModCategoryUpdate';
import { useProfile } from '../../../../shared/context/ProfileContext';
import { useModsStore } from '../../store/modsStore';
import { useMods } from '../../hooks/useMods';
import { categoryService } from '../../../../shared/services/categoryService';
import { useTranslation } from 'react-i18next';
import { useDelayedLoading } from '../../../../shared/hooks/useDelayedLoading';
import './CategoryPanel.css';

const { Sider } = Layout;

interface CategoryPanelProps {
  onSelect: (node: CategoryInfo | undefined) => void; // Coordination callback with load logic
  onRefreshTree: () => Promise<void>;
  onModsRefresh?: () => Promise<void>;
  unclassifiedCount: number;
  onUnclassifiedClick: () => void;
}

/**
 * CategoryPanel
 *
 * NEW ARCHITECTURE:
 * - Subscribes to its own state from useModsStore
 * - Receives coordination callbacks from parent (onSelect includes load logic)
 * - Much cleaner - reduced from 13 props to 5!
 */
export const CategoryPanel: React.FC<CategoryPanelProps> = ({
  onSelect,
  onRefreshTree,
  onModsRefresh,
  unclassifiedCount,
  onUnclassifiedClick,
}) => {
  // Subscribe to state this component needs
  const tree = useModsStore(s => s.CategoryTree);
  const loading = useModsStore(s => s.CategoryLoading);
  const selectedNode = useModsStore(s => s.selectedCategory);
  const searchQuery = useModsStore(s => s.categorySearch);
  const expandedKeys = useModsStore(s => s.expandedKeys);

  // Get operations
  const { setcategorySearch, setExpandedKeys } = useMods();

  // Is unclassified selected?
  const isUnclassifiedSelected = selectedNode?.id === "__unclassified__";
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();
  const { openCategoryScreen } = useCategoryScreen();
  const { updateModCategory } = useModCategoryUpdate({ onRefreshTree });
  const { loading: delayedLoading, execute } = useDelayedLoading(100); // Show loading only if operation takes >100ms

  const handleAddCategory = (parentId?: string) => {
    openCategoryScreen({
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
            // Create the Category node via backend
            const createdNode = await categoryService.createCategory(
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
              notification.success(t('category.createSuccess', { name: data.name }));
              // Refresh the Category tree to show the new node
              await onRefreshTree();
            } else {
              notification.error(t('category.createFailed', { name: data.name }));
            }
          } catch (error: unknown) {
            const errorMessage = error instanceof Error ? error.message : 'Unknown error';
            notification.error(t('category.createError', { error: errorMessage }));
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
      width="100%"
      className="category-panel-sider"
    >
      {/* Tree container with flex constraint to allow scrolling */}
      <div className="category-panel-tree-container">
        <CategoryTree
          tree={tree}
          loading={loading || delayedLoading}
          selectedNode={selectedNode}
          onSelect={onSelect}
          searchQuery={searchQuery}
          onSearchChange={setcategorySearch}
          expandedKeys={expandedKeys}
          onExpandedKeysChange={setExpandedKeys}
          onRefreshTree={onRefreshTree}
          onModsRefresh={onModsRefresh}
          onAddCategory={handleAddCategory}
        />
      </div>

      {/* Unclassified Item - fixed at bottom, doesn't scroll */}
      <div className="category-panel-unclassified-container">
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

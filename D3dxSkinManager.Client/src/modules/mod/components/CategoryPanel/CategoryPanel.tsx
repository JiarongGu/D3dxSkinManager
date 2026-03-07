import { notification } from '../../../../shared/utils/notification';
import React, { useCallback } from 'react';
import { Layout } from 'antd';
import { CategoryInfo, CATEGORY_IDS } from '../../../../shared/types/category.types';
import { CategoryTree } from './CategoryTree';
import { UnclassifiedItem } from './UnclassifiedItem';
import { useCategoryScreen } from './CategoryScreen';
import { useModCategoryUpdate } from './useModCategoryUpdate';
import { useProfile } from '../../../../shared/context/ProfileContext';
import { useModsStore } from '../../store/modsStore';
import { useMods } from '../../hooks/useMods';
import { categoryService } from '../../../../shared/services/ipc';
import { useTranslation } from 'react-i18next';
import { useDelayedLoading } from '../../../../shared/hooks/useDelayedLoading';
import './CategoryPanel.css';

const { Sider } = Layout;

interface CategoryPanelProps {
  // No props needed - component is fully self-contained!
}

/**
 * CategoryPanel
 *
 * SELF-CONTAINED ARCHITECTURE:
 * - Subscribes to its own state from useModsStore
 * - Gets operations from useMods hook
 * - No props needed - fully autonomous!
 */
export const CategoryPanel: React.FC<CategoryPanelProps> = () => {
  // Subscribe to state this component needs
  const tree = useModsStore(s => s.categoryTree);
  const loading = useModsStore(s => s.categoryLoading);
  const selectedNode = useModsStore(s => s.selectedCategory);
  const searchQuery = useModsStore(s => s.categorySearch);
  const expandedKeys = useModsStore(s => s.expandedKeys);
  const unclassifiedCount = useModsStore(s => s.unclassifiedCount);

  // Get operations
  const { setcategorySearch, setExpandedKeys, selectCategory, setSearchQuery } = useMods();

  // Is unclassified selected?
  const isUnclassifiedSelected = selectedNode?.id === CATEGORY_IDS.UNCLASSIFIED;
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();
  const { openCategoryScreen } = useCategoryScreen();
  const { updateModCategory } = useModCategoryUpdate();
  const { loading: delayedLoading, execute } = useDelayedLoading(200); // Show loading only if operation takes >100ms

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
              data.thumbnail
            );

            if (createdNode) {
              notification.success(t('category.createSuccess', { name: data.name }));
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
    // Note: Backend emits CATEGORY_UPDATED event, ModProvider handles refresh automatically
  }, [updateModCategory]);

  // Handle category selection
  const handleCategorySelect = useCallback((node: CategoryInfo | undefined) => {
    // Clear search when category changes
    setSearchQuery('');

    if (node) {
      void selectCategory(node.id);
    } else {
      // Clear category filter
      useModsStore.getState().setSelectedCategory(undefined);
      useModsStore.getState().setMods([]);
    }
  }, [setSearchQuery, selectCategory]);

  // Handle unclassified click
  const handleUnclassifiedClick = useCallback(() => {
    setSearchQuery('');
    void selectCategory(CATEGORY_IDS.UNCLASSIFIED);
  }, [setSearchQuery, selectCategory]);

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
          onSelect={handleCategorySelect}
          searchQuery={searchQuery}
          onSearchChange={setcategorySearch}
          expandedKeys={expandedKeys}
          onExpandedKeysChange={setExpandedKeys}
          onAddCategory={handleAddCategory}
        />
      </div>

      {/* Unclassified Item - fixed at bottom, doesn't scroll */}
      <div className="category-panel-unclassified-container">
        <UnclassifiedItem
          count={unclassifiedCount}
          isSelected={isUnclassifiedSelected}
          onClick={handleUnclassifiedClick}
          onModDrop={handleUnclassifiedDrop}
        />
      </div>
    </Sider>
  );
};

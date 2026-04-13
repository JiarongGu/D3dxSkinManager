import { notification } from '../../../../shared/utils/notification';
import React, { useCallback, useState } from 'react';
import { Layout, Button, Tooltip } from 'antd';
import { ApartmentOutlined, AppstoreOutlined, CheckCircleOutlined, UnorderedListOutlined } from '@ant-design/icons';
import { CategoryInfo, CATEGORY_IDS } from '../../../../shared/types/category.types';
import { CategoryTree } from './CategoryTree';
import { UnclassifiedItem } from './UnclassifiedItem';
import { useCategoryScreen } from './CategoryScreen';
import { useModCategoryUpdate } from './useModCategoryUpdate';
import { useProfile } from '../../../../shared/context/ProfileContext';
import { useModsStore } from '../../store/modsStore';
import { useMods } from '../../hooks/useMods';
import { categoryService, profileService } from '../../../../shared/services/ipc';
import { useTranslation } from 'react-i18next';
import { useDelayedLoading } from '../../../../shared/hooks/useDelayedLoading';
import { ModPackageTool } from '../../../tool/components/ModPackageTool/ModPackageTool';
import { ModAnalyzerTool } from '../../../tool/components/ModAnalyzerTool/ModAnalyzerTool';
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

  // Category view mode (tree/grid)
  const categoryViewMode = useModsStore(s => s.categoryViewMode);
  const setCategoryViewMode = useModsStore(s => s.setCategoryViewMode);

  // Get operations
  const { setcategorySearch, setExpandedKeys, selectCategory, loadAllMods, loadLoadedMods } = useMods();

  // Is unclassified selected?
  const isUnclassifiedSelected = selectedNode?.id === CATEGORY_IDS.UNCLASSIFIED;
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();
  const { openCategoryScreen } = useCategoryScreen();
  const { updateModCategory } = useModCategoryUpdate();
  const { loading: delayedLoading, execute } = useDelayedLoading(200); // Show loading only if operation takes >100ms

  // Export category state
  const [exportCategoryId, setExportCategoryId] = useState<string>();
  // Analyze category state
  const [analyzeCategoryId, setAnalyzeCategoryId] = useState<string>();

  const handleExportCategory = useCallback((nodeId: string) => {
    setExportCategoryId(nodeId);
  }, []);

  const handleAnalyzeCategory = useCallback((nodeId: string) => {
    setAnalyzeCategoryId(nodeId);
  }, []);

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
  const handleUnclassifiedDrop = useCallback(async (id?: string) => {
    if (!id) {
      return;
    }
    // Pass empty string for modName since we don't have it here
    // The updateModCategory function uses it only for the success message
    await updateModCategory(id, '', 'Unclassified');
    // Note: Backend emits CATEGORY_UPDATED event, ModProvider handles refresh automatically
  }, [updateModCategory]);

  // Handle category selection
  const handleCategorySelect = useCallback((node: CategoryInfo | undefined) => {
    if (node) {
      void selectCategory(node.id);
    } else {
      // Clear category filter
      useModsStore.getState().setSelectedCategory(undefined);
      useModsStore.getState().setMods([]);
    }
  }, [selectCategory]);

  // Handle unclassified click
  const handleUnclassifiedClick = useCallback(() => {
    void selectCategory(CATEGORY_IDS.UNCLASSIFIED);
  }, [selectCategory]);

  // Handle show all mods
  const handleShowAllMods = useCallback(() => {
    if (loadAllMods) {
      void loadAllMods();
    }
  }, [loadAllMods]);

  // Handle show loaded mods
  const handleShowLoadedMods = useCallback(() => {
    if (loadLoadedMods) {
      void loadLoadedMods();
    }
  }, [loadLoadedMods]);

  // Handle view mode toggle — update store and persist to profile config
  const handleToggleViewMode = useCallback(() => {
    const newMode = categoryViewMode === 'tree' ? 'grid' : 'tree';
    setCategoryViewMode(newMode);
    if (selectedProfileId) {
      void profileService.updateCategoryViewMode(selectedProfileId, newMode);
    }
  }, [categoryViewMode, setCategoryViewMode, selectedProfileId]);

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
          onExportCategory={handleExportCategory}
          onAnalyzeCategory={handleAnalyzeCategory}
        />
      </div>

      {/* Status Bar - split into left (unclassified) and right (action buttons) */}
      <div className="category-panel-unclassified-container">
        {/* Left section - Unclassified Item */}
        <div className="category-panel-status-left">
          <UnclassifiedItem
            count={unclassifiedCount}
            isSelected={isUnclassifiedSelected}
            onClick={handleUnclassifiedClick}
            onModDrop={handleUnclassifiedDrop}
          />
        </div>

        {/* Right section - Action icon buttons */}
        <div className="category-panel-status-right">
          <Tooltip title={t(categoryViewMode === 'tree' ? 'category.gridView' : 'category.treeView')} placement="top">
            <Button
              type="text"
              size="small"
              icon={categoryViewMode === 'tree' ? <AppstoreOutlined /> : <ApartmentOutlined />}
              onClick={handleToggleViewMode}
              className="category-panel-action-button"
            />
          </Tooltip>
          <Tooltip title={t('category.showAllMods')} placement="top">
            <Button
              type="text"
              size="small"
              icon={<UnorderedListOutlined />}
              onClick={handleShowAllMods}
              className="category-panel-action-button"
            />
          </Tooltip>
          <Tooltip title={t('category.showLoadedMods')} placement="top">
            <Button
              type="text"
              size="small"
              icon={<CheckCircleOutlined />}
              onClick={handleShowLoadedMods}
              className="category-panel-action-button"
            />
          </Tooltip>
        </div>
      </div>
      {/* Export Category - Opens ModPackageTool with pre-selected mods */}
      <ModPackageTool
        visible={exportCategoryId !== undefined}
        onClose={() => setExportCategoryId(undefined)}
        initialCategoryId={exportCategoryId}
      />
      {/* Analyze Category - Opens ModAnalyzerTool with category pre-selected */}
      <ModAnalyzerTool
        visible={analyzeCategoryId !== undefined}
        onClose={() => setAnalyzeCategoryId(undefined)}
        initialCategoryId={analyzeCategoryId}
      />
    </Sider>
  );
};

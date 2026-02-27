import React, { useMemo } from 'react';
import { Layout, Empty, Input, Button, Tooltip } from 'antd';
import { SearchOutlined, PlusOutlined } from '@ant-design/icons';
import { ModInfo } from '../../../../shared/types/mod.types';
import { ModList } from './ModList';
import { ModListStatusBar } from './ModListStatusBar';
import { useModsStore } from '../../store/modsStore';
import { useMods } from '../../hooks/useMods';
import { useTranslation } from 'react-i18next';
import './ModListPanel.css';

const { Sider } = Layout;
const { Search } = Input;

/**
 * ModListPanel
 *
 * NEW ARCHITECTURE:
 * - Subscribes to its own state from useModsStore
 * - Gets operations from useMods()
 * - No props needed!
 */
export const ModListPanel: React.FC = () => {
  // Subscribe to state this component needs
  const mods = useModsStore(s => s.mods);
  const loading = useModsStore(s => s.modsLoading); // Mod list panel loading state
  const selectedMod = useModsStore(s => s.selectedMod);
  const searchQuery = useModsStore(s => s.searchQuery);
  const selectedCategory = useModsStore(s => s.selectedCategory);
  const selectedObject = useModsStore(s => s.selectedObject);
  const CategoryFilteredMods = useModsStore(s => s.CategoryFilteredMods);

  // Get operations
  const {
    setSearchQuery,
    loadModInGame,
    unloadModFromGame,
    deleteMod,
    openEditDialog,
    selectMod,
    openModManagementScreen,
  } = useMods();
  const { t } = useTranslation();
  const contentRef = React.useRef<HTMLDivElement>(null);

  // Compute filtered mods based on search and Category
  const filteredMods = useMemo(() => {
    // If a Category is selected, use Category-filtered mods
    let result: ModInfo[];
    if (selectedCategory) {
      result = CategoryFilteredMods || [];
    } else {
      result = mods;
    }

    // Filter by selected object (only if no Category is selected)
    if (!selectedCategory && selectedObject && selectedObject !== "all") {
      result = result.filter((mod: ModInfo) => mod.category === selectedObject);
    }

    // Apply mod search filter
    if (searchQuery) {
      const searchLower = searchQuery.toLowerCase();
      result = result.filter(
        (mod: ModInfo) =>
          mod.name.toLowerCase().includes(searchLower) ||
          (mod.author && mod.author.toLowerCase().includes(searchLower)) ||
          (mod.tags && mod.tags.some((tag: string) => tag.toLowerCase().includes(searchLower)))
      );
    }

    // Add "Unload" option at the beginning if object is selected and has loaded mod
    if (selectedObject && selectedObject !== "all") {
      const hasLoadedMod = result.some((mod: ModInfo) => mod.isLoaded);
      if (hasLoadedMod) {
        const unloadOption: ModInfo = {
          sha: "__UNLOAD__",
          name: "- [X] Unload This Object -",
          category: selectedObject,
          author: "",
          tags: [],
          grading: "",
          description: "",
          isLoaded: false,
          type: "special",
          isAvailable: true,
          hasCache: false,
        };
        result = [unloadOption, ...result];
      }
    }

    return result;
  }, [mods, CategoryFilteredMods, selectedObject, selectedCategory, searchQuery]);

  const handleLoadedModClick = (mod: ModInfo) => {
    // Scroll to the loaded mod and select it
    selectMod(mod);

    // Scroll the mod into view
    if (contentRef.current) {
      // Find the mod element by its SHA
      const modElement = contentRef.current.querySelector(`[data-mod-sha="${mod.sha}"]`);
      if (modElement) {
        modElement.scrollIntoView({ behavior: 'smooth', block: 'center' });
      }
    }
  };

  if (!selectedCategory && !selectedObject) {
    return (
      <Sider width="100%" className="mod-list-panel">
        <div className="mod-list-panel-empty-container">
          <Empty
            description={t('mods.panel.selectCategory')}
            image={Empty.PRESENTED_IMAGE_SIMPLE}
            className="mod-list-panel-empty"
          />
        </div>
      </Sider>
    );
  }

  return (
    <Sider width="100%" className="mod-list-panel-flex">
      {/* Search Bar with Add Button */}
      <div className="mod-list-panel-search-bar">
        <Search
          placeholder={t('mods.list.searchPlaceholder')}
          value={searchQuery}
          onChange={(e) => setSearchQuery(e.target.value)}
          allowClear
          prefix={<SearchOutlined />}
        />
        <Tooltip title={t('mods.panel.openImportQueue')} placement="top">
          <Button
            type="default"
            icon={<PlusOutlined />}
            onClick={() => openModManagementScreen()}
          />
        </Tooltip>
      </div>

      {/* Mod List or Empty State */}
      <div className="mod-list-panel-content" ref={contentRef}>
        {filteredMods.length > 0 ? (
          <ModList
            mods={filteredMods}
            loading={loading}
            onLoad={loadModInGame}
            onUnload={unloadModFromGame}
            onDelete={deleteMod}
            onEdit={openEditDialog}
            onRowClick={selectMod}
            selectedMod={selectedMod}
          />
        ) : (
          <div className="mod-list-panel-content-empty-container">
            <Empty
              description={
                searchQuery
                  ? t('mods.panel.noModsMatchingSearch', { query: searchQuery })
                  : selectedCategory
                    ? t('mods.panel.noModsForCategory', { name: selectedCategory.name })
                    : selectedObject
                      ? t('mods.panel.noModsForObject', { object: selectedObject })
                      : t('mods.panel.noModsAvailable')
              }
              image={Empty.PRESENTED_IMAGE_SIMPLE}
            />
          </div>
        )}
      </div>

      {/* Status Bar at Bottom - fixed container like UnclassifiedItem */}
      <div className="mod-list-panel-status-bar-container">
        <ModListStatusBar
          mods={filteredMods}
          selectedCategory={selectedCategory}
          selectedObject={selectedObject}
          onLoadedModClick={handleLoadedModClick}
        />
      </div>
    </Sider>
  );
};

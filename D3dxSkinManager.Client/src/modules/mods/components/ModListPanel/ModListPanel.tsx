import React from 'react';
import { Layout, Empty, Input } from 'antd';
import { SearchOutlined } from '@ant-design/icons';
import { ModInfo } from '../../../../shared/types/mod.types';
import { ClassificationNode } from '../../../../shared/types/classification.types';
import { ModList } from './ModList';
import { ModListStatusBar } from './ModListStatusBar';
import { useTranslation } from 'react-i18next';
import './ModListPanel.css';

const { Sider } = Layout;
const { Search } = Input;

interface ModListPanelProps {
  mods: ModInfo[];
  loading: boolean;
  selectedMod: ModInfo | undefined;
  searchQuery: string;
  onSearchChange: (query: string) => void;
  onLoad: (sha: string) => void;
  onUnload: (sha: string) => void;
  onDelete: (sha: string, name: string) => void;
  onEdit: (mod: ModInfo) => void;
  onRowClick: (mod: ModInfo) => void;
  selectedClassification: ClassificationNode | undefined;
  selectedObject: string;
}

export const ModListPanel: React.FC<ModListPanelProps> = ({
  mods,
  loading,
  selectedMod,
  searchQuery,
  onSearchChange,
  onLoad,
  onUnload,
  onDelete,
  onEdit,
  onRowClick,
  selectedClassification,
  selectedObject,
}) => {
  const { t } = useTranslation();
  const contentRef = React.useRef<HTMLDivElement>(null);

  const handleLoadedModClick = (mod: ModInfo) => {
    // Scroll to the loaded mod and select it
    onRowClick(mod);

    // Scroll the mod into view
    if (contentRef.current) {
      // Find the mod element by its SHA
      const modElement = contentRef.current.querySelector(`[data-mod-sha="${mod.sha}"]`);
      if (modElement) {
        modElement.scrollIntoView({ behavior: 'smooth', block: 'center' });
      }
    }
  };

  if (!selectedClassification && !selectedObject) {
    return (
      <Sider width={450} className="mod-list-panel">
        <div className="mod-list-panel-empty-container">
          <Empty
            description={t('mods.panel.selectClassification')}
            image={Empty.PRESENTED_IMAGE_SIMPLE}
            className="mod-list-panel-empty"
          />
        </div>
      </Sider>
    );
  }

  return (
    <Sider width={450} className="mod-list-panel-flex">
      {/* Search Bar */}
      <div className="mod-list-panel-search-bar">
        <Search
          placeholder={t('mods.list.searchPlaceholder')}
          value={searchQuery}
          onChange={(e) => onSearchChange(e.target.value)}
          allowClear
          prefix={<SearchOutlined />}
        />
      </div>

      {/* Mod List or Empty State */}
      <div className="mod-list-panel-content" ref={contentRef}>
        {mods.length > 0 ? (
          <ModList
            mods={mods}
            loading={loading}
            onLoad={onLoad}
            onUnload={onUnload}
            onDelete={onDelete}
            onEdit={onEdit}
            onRowClick={onRowClick}
            selectedMod={selectedMod}
          />
        ) : (
          <div className="mod-list-panel-content-empty-container">
            <Empty
              description={
                searchQuery
                  ? t('mods.panel.noModsMatchingSearch', { query: searchQuery })
                  : selectedClassification
                    ? t('mods.panel.noModsForClassification', { name: selectedClassification.name })
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
          mods={mods}
          selectedClassification={selectedClassification}
          selectedObject={selectedObject}
          onLoadedModClick={handleLoadedModClick}
        />
      </div>
    </Sider>
  );
};

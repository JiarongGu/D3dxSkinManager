import React from 'react';
import { Space } from 'antd';
import { ModInfo } from '../../../shared/types/mod.types';
import { ModSearchBar } from './ModSearchBar';
import { ModFilterPanel } from './ModFilterPanel';
import { ModTable } from './ModTable';

interface ModManagementViewProps {
  mods: ModInfo[];
  filteredMods: ModInfo[];
  loading: boolean;
  objects: string[];
  authors: string[];
  searchTerm: string;
  selectedObject: string;
  selectedGrading: string;
  hasActiveFilters: boolean;
  onSearchChange: (value: string) => void;
  onSearch: (value: string) => void;
  onObjectChange: (value: string) => void;
  onGradingChange: (value: string) => void;
  onClearFilters: () => void;
  onRefresh: () => void;
  onLoad: (sha: string) => void;
  onUnload: (sha: string) => void;
  onDelete: (sha: string, name: string) => void;
}

export const ModManagementView: React.FC<ModManagementViewProps> = ({
  mods,
  filteredMods,
  loading,
  objects,
  authors,
  searchTerm,
  selectedObject,
  selectedGrading,
  hasActiveFilters,
  onSearchChange,
  onSearch,
  onObjectChange,
  onGradingChange,
  onClearFilters,
  onRefresh,
  onLoad,
  onUnload,
  onDelete
}) => {
  const displayMods = hasActiveFilters ? filteredMods : mods;

  return (
    <>
      <div style={{ marginBottom: 16 }}>
        <Space wrap>
          <ModSearchBar
            value={searchTerm}
            onChange={onSearchChange}
            onSearch={onSearch}
          />
          <ModFilterPanel
            selectedObject={selectedObject}
            selectedGrading={selectedGrading}
            objects={objects}
            loading={loading}
            onObjectChange={onObjectChange}
            onGradingChange={onGradingChange}
            onClearFilters={onClearFilters}
            onRefresh={onRefresh}
          />
        </Space>
      </div>
      <ModTable
        mods={displayMods}
        loading={loading}
        objects={objects}
        authors={authors}
        onLoad={onLoad}
        onUnload={onUnload}
        onDelete={onDelete}
      />
    </>
  );
};

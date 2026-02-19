import React from 'react';
import { Layout, Empty, Input } from 'antd';
import { SearchOutlined } from '@ant-design/icons';
import { ModInfo } from '../../../../shared/types/mod.types';
import { ClassificationNode } from '../../../../shared/types/classification.types';
import { ModList } from './ModList';

const { Sider } = Layout;
const { Search } = Input;

interface ModListPanelProps {
  mods: ModInfo[];
  loading: boolean;
  selectedMod: ModInfo | null;
  searchQuery: string;
  onSearchChange: (query: string) => void;
  onLoad: (sha: string) => void;
  onUnload: (sha: string) => void;
  onDelete: (sha: string, name: string) => void;
  onEdit: (mod: ModInfo) => void;
  onRowClick: (mod: ModInfo) => void;
  selectedClassification: ClassificationNode | null;
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
  if (!selectedClassification && !selectedObject) {
    return (
      <Sider
        width={450}
        style={{
          background: 'var(--color-bg-elevated) !important',
          height: '100%',
        }}
      >
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            height: '100%',
            width: '100%',
          }}
        >
          <Empty
            description="Select a classification to view mods"
            image={Empty.PRESENTED_IMAGE_SIMPLE}
            style={{ margin: 0 }}
          />
        </div>
      </Sider>
    );
  }

  return (
    <Sider
      width={450}
      style={{
        background: 'var(--color-bg-elevated) !important',
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        overflow: 'hidden',
      }}
    >
      {/* Search Bar */}
      <div
        style={{
          padding: '8px',
          borderBottom: '1px solid var(--color-border-secondary)',
          flexShrink: 0,
          height: '48px',
        }}
      >
        <Search
          placeholder="Search mods (name, author, tags)..."
          value={searchQuery}
          onChange={(e) => onSearchChange(e.target.value)}
          allowClear
          prefix={<SearchOutlined />}
        />
      </div>

      {/* Mod List or Empty State */}
      <div style={{ flex: 1, height: 'calc(100% - 48px)', overflow: 'auto' }}>
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
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              height: '100%',
            }}
          >
            <Empty
              description={
                searchQuery
                  ? `No mods found matching "${searchQuery}"`
                  : selectedClassification
                    ? `No mods found for ${selectedClassification.name}`
                    : selectedObject
                      ? `No mods found for ${selectedObject}`
                      : 'No mods available'
              }
              image={Empty.PRESENTED_IMAGE_SIMPLE}
            />
          </div>
        )}
      </div>
    </Sider>
  );
};

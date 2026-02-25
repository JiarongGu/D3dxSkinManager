import React from 'react';
import { CheckCircleOutlined, MinusCircleOutlined } from '@ant-design/icons';
import { ModInfo } from '../../../../shared/types/mod.types';
import { CategoryInfo } from '../../../../shared/types/category.types';
import './ModListStatusBar.css';

export interface ModListStatusBarProps {
  mods: ModInfo[];
  selectedCategory: CategoryInfo | undefined;
  selectedObject: string;
  onLoadedModClick?: (mod: ModInfo) => void;
}

export const ModListStatusBar: React.FC<ModListStatusBarProps> = ({
  mods,
  selectedCategory,
  selectedObject,
  onLoadedModClick,
}) => {
  // Find the loaded mod in the current mod list
  const loadedMod = mods.find(mod => mod.isLoaded);

  const handleClick = () => {
    if (loadedMod && onLoadedModClick) {
      onLoadedModClick(loadedMod);
    }
  };

  return (
    <div
      className={`mod-list-status-bar ${loadedMod ? 'mod-list-status-bar-clickable' : ''}`}
      onClick={handleClick}
    >
      <div className="mod-list-status-bar-content">
        {loadedMod ? (
          <>
            <CheckCircleOutlined className="mod-list-status-bar-icon mod-list-status-bar-icon-active" />
            <span className="mod-list-status-bar-text mod-list-status-bar-text-active">{loadedMod.name}</span>
          </>
        ) : (
          <>
            <MinusCircleOutlined className="mod-list-status-bar-icon mod-list-status-bar-icon-inactive" />
            <span className="mod-list-status-bar-text mod-list-status-bar-text-inactive">No active mod</span>
          </>
        )}
      </div>
    </div>
  );
};

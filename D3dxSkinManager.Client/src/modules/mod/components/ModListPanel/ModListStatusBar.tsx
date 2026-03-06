import React from 'react';
import classNames from 'classnames';
import { CheckCircleOutlined, MinusCircleOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { ModInfo } from '../../../../shared/types/mod.types';
import './ModListStatusBar.css';

export interface ModListStatusBarProps {
  mods: ModInfo[];
  onLoadedModClick?: (mod: ModInfo) => void;
  selectedModCount?: number;
}

export const ModListStatusBar: React.FC<ModListStatusBarProps> = ({
  mods,
  onLoadedModClick,
  selectedModCount = 0,
}) => {
  const { t } = useTranslation();

  // Find the loaded mod in the current mod list
  const loadedMod = mods.find(mod => mod.isLoaded);

  const handleClick = () => {
    if (loadedMod && onLoadedModClick) {
      onLoadedModClick(loadedMod);
    }
  };

  // Show selection count if multiple mods are selected
  const showSelectionCount = selectedModCount > 1;

  return (
    <div
      className={classNames('mod-list-status-bar', { 'mod-list-status-bar-clickable': loadedMod })}
      onClick={handleClick}
    >
      <div className="mod-list-status-bar-content">
        {showSelectionCount ? (
          <>
            <CheckCircleOutlined className="mod-list-status-bar-icon mod-list-status-bar-icon-active" />
            <span className="mod-list-status-bar-text mod-list-status-bar-text-active">
              {t('mods.panel.statusBar.modsSelected', { count: selectedModCount })}
            </span>
          </>
        ) : loadedMod ? (
          <>
            <CheckCircleOutlined className="mod-list-status-bar-icon mod-list-status-bar-icon-active" />
            <span className="mod-list-status-bar-text mod-list-status-bar-text-active">{loadedMod.name}</span>
          </>
        ) : (
          <>
            <MinusCircleOutlined className="mod-list-status-bar-icon mod-list-status-bar-icon-inactive" />
            <span className="mod-list-status-bar-text mod-list-status-bar-text-inactive">
              {t('mods.panel.statusBar.noActiveMod')}
            </span>
          </>
        )}
      </div>
    </div>
  );
};

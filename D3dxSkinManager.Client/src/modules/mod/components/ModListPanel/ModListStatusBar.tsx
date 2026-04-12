import React, { useState, useRef, useEffect, useCallback, useMemo } from 'react';
import classNames from 'classnames';
import { CheckCircleOutlined, MinusCircleOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { CountBadge } from '../../../../shared/components/common';
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
  const [expanded, setExpanded] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  const loadedMods = useMemo(() => mods.filter(mod => mod.isLoaded), [mods]);

  const hasMultiple = loadedMods.length > 1;
  const hasSingle = loadedMods.length === 1;
  const hasLoaded = loadedMods.length > 0;

  // Close dropdown on outside click
  useEffect(() => {
    if (!expanded) return;

    const handleClickOutside = (e: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setExpanded(false);
      }
    };

    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, [expanded]);

  // Close dropdown when loaded mods drop to 0 or 1
  useEffect(() => {
    if (loadedMods.length <= 1) {
      setExpanded(false);
    }
  }, [loadedMods.length]);

  const handleBarClick = useCallback(() => {
    if (hasSingle && onLoadedModClick) {
      onLoadedModClick(loadedMods[0]);
    } else if (hasMultiple) {
      setExpanded(prev => !prev);
    }
  }, [hasSingle, hasMultiple, loadedMods, onLoadedModClick]);

  const handleModItemClick = useCallback((mod: ModInfo) => {
    setExpanded(false);
    onLoadedModClick?.(mod);
  }, [onLoadedModClick]);

  // Show selection count if multiple mods are selected (takes priority)
  const showSelectionCount = selectedModCount > 1;

  return (
    <div className="mod-list-status-bar-wrapper" ref={containerRef}>
      {/* Expanded dropdown - renders above the bar */}
      {expanded && hasMultiple && (
        <div className="mod-list-status-bar-dropdown">
          {loadedMods.map(mod => (
            <div
              key={mod.id}
              className="mod-list-status-bar-dropdown-item"
              onClick={() => handleModItemClick(mod)}
            >
              <CheckCircleOutlined className="mod-list-status-bar-dropdown-item-icon" />
              <div className="mod-list-status-bar-dropdown-item-info">
                <span className="mod-list-status-bar-dropdown-item-name">{mod.name}</span>
                {mod.categoryName && (
                  <span className="mod-list-status-bar-dropdown-item-category">{mod.categoryName}</span>
                )}
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Main status bar */}
      <div
        className={classNames('mod-list-status-bar', {
          'mod-list-status-bar-clickable': hasLoaded,
          'mod-list-status-bar-expanded': expanded,
        })}
        onClick={handleBarClick}
      >
        <div className="mod-list-status-bar-content">
          {showSelectionCount ? (
            <>
              <CheckCircleOutlined className="mod-list-status-bar-icon mod-list-status-bar-icon-active" />
              <span className="mod-list-status-bar-text mod-list-status-bar-text-active">
                {t('mods.panel.statusBar.modsSelected', { count: selectedModCount })}
              </span>
            </>
          ) : hasMultiple ? (
            <>
              <CheckCircleOutlined className="mod-list-status-bar-icon mod-list-status-bar-icon-active" />
              <span className="mod-list-status-bar-text mod-list-status-bar-text-active">
                {loadedMods.map(m => m.name).join(', ')}
              </span>
            </>
          ) : hasSingle ? (
            <>
              <CheckCircleOutlined className="mod-list-status-bar-icon mod-list-status-bar-icon-active" />
              <span className="mod-list-status-bar-text mod-list-status-bar-text-active">{loadedMods[0].name}</span>
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
        {hasMultiple && <CountBadge count={loadedMods.length} />}
      </div>
    </div>
  );
};

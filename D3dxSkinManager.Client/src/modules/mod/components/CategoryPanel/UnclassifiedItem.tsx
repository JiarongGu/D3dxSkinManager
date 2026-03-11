import React from 'react';
import { useTranslation } from 'react-i18next';
import classNames from 'classnames';
import { Badge } from 'antd';
import { AppstoreOutlined } from '@ant-design/icons';
import { useDragDrop } from '../../../../shared/hooks/useDragDrop';
import './UnclassifiedItem.css';

export interface UnclassifiedItemProps {
  count: number;
  isSelected: boolean;
  onClick: () => void;
  onModDrop?: (id?: string) => void;
}

export const UnclassifiedItem: React.FC<UnclassifiedItemProps> = ({
  count,
  isSelected,
  onClick,
  onModDrop,
}) => {
  const { t } = useTranslation();

  // Use the unified drag-and-drop hook
  const { containerRef } = useDragDrop<HTMLDivElement>(
    {
      eventType: 'application/mod-id',
      allow: 'node', // Allow dropping into the unclassified area
      nodeSelector: '.category-panel-unclassified-item', // Target the entire item
      onDrop: ({ data }) => {
        if (onModDrop) {
          onModDrop(data);
        }
        return true;
      }
    }
  );

  return (
    <div
      ref={(el) => containerRef(el || undefined)}
      className={classNames('category-panel-unclassified-item', { 'category-panel-unclassified-item--selected': isSelected })}
      onClick={onClick}
      role="button"
      tabIndex={0}
      onKeyDown={(e) => {
        if (e.key === 'Enter' || e.key === ' ') {
          onClick();
        }
      }}
    >
      <div className="category-panel-unclassified-item-content">
        <AppstoreOutlined className="category-panel-unclassified-item-icon" />
        <span className="category-panel-unclassified-item-text">{t('category.unclassified')}</span>
      </div>
      <Badge
        count={count}
        showZero
        className="category-panel-unclassified-item-badge"
      />
    </div>
  );
};

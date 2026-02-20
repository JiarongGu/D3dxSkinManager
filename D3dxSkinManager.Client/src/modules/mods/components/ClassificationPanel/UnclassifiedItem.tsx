import React from 'react';
import { Badge } from 'antd';
import { AppstoreOutlined } from '@ant-design/icons';
import { useDragDrop } from '../../../../shared/hooks/useDragDrop';
import './UnclassifiedItem.css';

export interface UnclassifiedItemProps {
  count: number;
  isSelected: boolean;
  onClick: () => void;
  onModDrop?: (sha?: string) => void;
}

export const UnclassifiedItem: React.FC<UnclassifiedItemProps> = ({
  count,
  isSelected,
  onClick,
  onModDrop,
}) => {
  // Use the unified drag-and-drop hook
  const { containerRef } = useDragDrop<HTMLDivElement>(
    {
      eventType: 'application/mod-sha',
      allow: 'node', // Allow dropping into the unclassified area
      nodeSelector: '.unclassified-item', // Target the entire item
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
      ref={containerRef}
      className={`unclassified-item ${isSelected ? 'selected' : ''}`}
      onClick={onClick}
      role="button"
      tabIndex={0}
      onKeyDown={(e) => {
        if (e.key === 'Enter' || e.key === ' ') {
          onClick();
        }
      }}
    >
      <div className="unclassified-item-content">
        <AppstoreOutlined className="unclassified-item-icon" />
        <span className="unclassified-item-text">Unclassified</span>
      </div>
      <Badge
        count={count}
        showZero
        className="unclassified-item-badge"
      />
    </div>
  );
};

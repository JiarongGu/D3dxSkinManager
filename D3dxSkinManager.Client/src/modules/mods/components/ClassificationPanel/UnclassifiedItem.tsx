import React, { useState } from 'react';
import { Badge } from 'antd';
import { AppstoreOutlined } from '@ant-design/icons';
import './UnclassifiedItem.css';

export interface UnclassifiedItemProps {
  count: number;
  isSelected: boolean;
  onClick: () => void;
  onModDrop?: (e: React.DragEvent) => void;
}

export const UnclassifiedItem: React.FC<UnclassifiedItemProps> = ({
  count,
  isSelected,
  onClick,
  onModDrop,
}) => {
  const [isDragOver, setIsDragOver] = useState(false);

  const handleDragOver = (e: React.DragEvent) => {
    // Check if mod is being dragged
    const types = Array.from(e.dataTransfer?.types || []);
    if (types.includes('application/mod-sha')) {
      e.preventDefault();
      e.dataTransfer.dropEffect = 'move';
      setIsDragOver(true);
    }
  };

  const handleDragLeave = () => {
    setIsDragOver(false);
  };

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragOver(false);
    const modSha = e.dataTransfer.getData('application/mod-sha');
    if (modSha && onModDrop) {
      onModDrop(e);
    }
  };

  return (
    <div
      className={`unclassified-item ${isSelected ? 'selected' : ''} ${isDragOver ? 'drag-over' : ''}`}
      onClick={onClick}
      role="button"
      tabIndex={0}
      onKeyDown={(e) => {
        if (e.key === 'Enter' || e.key === ' ') {
          onClick();
        }
      }}
      onDragOver={handleDragOver}
      onDragLeave={handleDragLeave}
      onDrop={handleDrop}
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

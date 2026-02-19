import React from 'react';
import { Badge } from 'antd';
import { AppstoreOutlined } from '@ant-design/icons';
import './UnclassifiedItem.css';

export interface UnclassifiedItemProps {
  count: number;
  isSelected: boolean;
  onClick: () => void;
}

export const UnclassifiedItem: React.FC<UnclassifiedItemProps> = ({
  count,
  isSelected,
  onClick,
}) => {
  return (
    <div
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

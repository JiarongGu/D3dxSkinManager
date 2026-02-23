import React from 'react';
import { CheckCircleOutlined, CloseCircleOutlined } from '@ant-design/icons';
import './StatusIcon.css';

export interface StatusIconProps {
  isLoaded: boolean;
}

export const StatusIcon: React.FC<StatusIconProps> = ({ isLoaded }) => {
  return isLoaded ? (
    <CheckCircleOutlined className="status-icon-loaded" />
  ) : (
    <CloseCircleOutlined className="status-icon-not-loaded" />
  );
};

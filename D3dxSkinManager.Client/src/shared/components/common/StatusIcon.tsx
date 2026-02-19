import React from 'react';
import { CheckCircleOutlined, CloseCircleOutlined } from '@ant-design/icons';

export interface StatusIconProps {
  isLoaded: boolean;
}

export const StatusIcon: React.FC<StatusIconProps> = ({ isLoaded }) => {
  return isLoaded ? (
    <CheckCircleOutlined style={{ color: '#52c41a', fontSize: '20px' }} />
  ) : (
    <CloseCircleOutlined style={{ color: '#d9d9d9', fontSize: '20px' }} />
  );
};

import React from 'react';
import {
  CheckCircleOutlined,
  CloseCircleOutlined,
  WarningOutlined,
  InfoCircleOutlined,
} from '@ant-design/icons';

interface HealthStatusIconProps {
  status: string;
  size?: number;
}

/**
 * Renders a colored icon for health/severity status values.
 * Supports: 'error' (red), 'warning' (orange), 'info' (blue), anything else (green/healthy).
 */
export const HealthStatusIcon: React.FC<HealthStatusIconProps> = ({ status, size = 12 }) => {
  const style = { fontSize: size };

  if (status === 'error') return <CloseCircleOutlined style={{ ...style, color: 'var(--color-error)' }} />;
  if (status === 'warning') return <WarningOutlined style={{ ...style, color: 'var(--color-warning)' }} />;
  if (status === 'info') return <InfoCircleOutlined style={{ ...style, color: 'var(--color-primary)' }} />;
  return <CheckCircleOutlined style={{ ...style, color: 'var(--color-success)' }} />;
};

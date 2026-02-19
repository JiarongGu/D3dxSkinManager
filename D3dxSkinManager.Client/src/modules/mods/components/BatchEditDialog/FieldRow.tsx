import React from 'react';
import { Space, Checkbox } from 'antd';

export interface FieldRowProps {
  checked: boolean;
  onToggle: () => void;
  children: React.ReactNode;
}

/**
 * Reusable field row component for batch editing
 * Shows a checkbox on the left and the form field on the right
 */
export const FieldRow: React.FC<FieldRowProps> = ({
  checked,
  onToggle,
  children,
}) => {
  return (
    <Space align="start" style={{ width: '100%' }}>
      <Checkbox
        checked={checked}
        onChange={onToggle}
        style={{ marginTop: '30px' }}
      />
      {children}
    </Space>
  );
};

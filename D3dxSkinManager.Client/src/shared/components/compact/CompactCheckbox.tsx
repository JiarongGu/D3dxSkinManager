import React from 'react';
import { Checkbox, CheckboxProps } from 'antd';

/**
 * CompactCheckbox - L1 atom: antd Checkbox with the app's consistent 14px type + class hook.
 *
 * Layer: L1 (pure visual). Use this instead of a raw antd Checkbox so checkbox controls share one
 * style across the app (per ui-component-layers.md). Forwards all antd Checkbox props.
 *
 * Usage:
 *   <CompactCheckbox checked={value} onChange={(e) => set(e.target.checked)}>Label</CompactCheckbox>
 */
export interface CompactCheckboxProps extends CheckboxProps {
  className?: string;
}

export const CompactCheckbox: React.FC<CompactCheckboxProps> = ({ className, ...rest }) => {
  const combined = className ? `compact-checkbox ${className}` : 'compact-checkbox';
  return <Checkbox className={combined} {...rest} />;
};

export default CompactCheckbox;

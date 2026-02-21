import React from 'react';
import { Select, SelectProps } from 'antd';
import './CompactSelect.css';

/**
 * CompactSelect - Select component with consistent compact sizing
 *
 * Features:
 * - Consistent sizing across the application (default: 'medium' - 32px)
 * - Maintains all Ant Design Select props and functionality
 * - Centralized styling management
 *
 * Usage:
 * <CompactSelect options={options} />
 * <CompactSelect size="small" mode="multiple" />
 */

export type CompactSelectSize = 'small' | 'medium' | 'large';

export interface CompactSelectProps<T = any> extends Omit<SelectProps<T>, 'size'> {
  /** Select size - defaults to 'medium' (32px) for consistency */
  size?: CompactSelectSize;
}

export function CompactSelect<T = any>({
  size = 'medium',
  className = '',
  ...rest
}: CompactSelectProps<T>) {
  // Map our size to Ant Design size
  const antdSize = size === 'medium' ? 'middle' : size;

  // Build className with size-specific class
  const selectClassName = `compact-select compact-select-${size} ${className}`.trim();

  return (
    <Select<T> size={antdSize} className={selectClassName} {...rest} />
  );
}

export default CompactSelect;

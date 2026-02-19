import React from 'react';
import { Divider, DividerProps } from 'antd';

/**
 * CompactDivider - A divider component with reduced margins for compact layouts
 *
 * Features:
 * - Reduced top and bottom margins (12px vs default 24px)
 * - Consistent spacing across the application
 * - Maintains all Ant Design Divider props and functionality
 *
 * Usage:
 * <CompactDivider />
 * <CompactDivider orientation="left">Section Title</CompactDivider>
 */

export interface CompactDividerProps extends Omit<DividerProps, 'style'> {
  /** Additional inline styles to apply to the divider */
  style?: React.CSSProperties;
  /** Whether to use extra compact spacing */
  extraCompact?: boolean;
}

export const CompactDivider: React.FC<CompactDividerProps> = ({
  style,
  extraCompact = false,
  ...rest
}) => {
  const defaultStyle: React.CSSProperties = {
    margin: extraCompact ? '8px 0' : '12px 0',
    ...style,
  };

  return <Divider style={defaultStyle} {...rest} />;
};

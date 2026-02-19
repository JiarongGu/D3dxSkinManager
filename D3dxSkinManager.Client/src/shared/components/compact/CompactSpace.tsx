import React from 'react';
import { Space, SpaceProps } from 'antd';

/**
 * CompactSpace - A space component with reduced spacing for compact layouts
 *
 * Features:
 * - Defaults to 'small' size for tighter spacing
 * - Consistent spacing across the application
 * - Maintains all Ant Design Space props and functionality
 *
 * Usage:
 * <CompactSpace vertical>
 *   <div>Item 1</div>
 *   <div>Item 2</div>
 * </CompactSpace>
 */

export interface CompactSpaceProps extends SpaceProps {
  /** Override the default 'small' size if needed */
  size?: SpaceProps['size'];
  /** Use vertical prop instead of direction="vertical" (deprecated) */
  vertical?: boolean;
}

export const CompactSpace: React.FC<CompactSpaceProps> = ({
  size = 'small',
  vertical,
  children,
  ...rest
}) => {
  // Support both old direction prop and new orientation prop for backward compatibility
  const orientation = vertical ? 'vertical' : rest.direction;

  return (
    <Space size={size} orientation={orientation} {...rest}>
      {children}
    </Space>
  );
};

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
  /** Render Ant's Space.Compact — attached form controls that share borders (e.g. input + button). */
  compact?: boolean;
  /** Fill the container width (Space.Compact `block`). Only meaningful with `compact`. */
  block?: boolean;
}

export const CompactSpace: React.FC<CompactSpaceProps> = ({
  size = 'small',
  vertical,
  compact,
  children,
  className,
  style,
  block,
  direction,
  ...rest
}) => {
  // antd Space uses `direction` (there is no `orientation` prop). The `vertical` shorthand wins;
  // otherwise honour an explicit `direction`. Destructure `direction` out of `...rest` so the spread
  // below can't re-inject `direction={undefined}` and clobber a vertical layout.
  const resolvedDirection = vertical ? 'vertical' : direction;

  // Space.Compact groups controls into one attached unit (shared borders) — a distinct antd component
  // from spaced Space, so route to it explicitly rather than passing an unsupported `size`.
  if (compact) {
    return (
      <Space.Compact direction={vertical ? 'vertical' : 'horizontal'} block={block} className={className} style={style}>
        {children}
      </Space.Compact>
    );
  }

  return (
    <Space size={size} direction={resolvedDirection} className={className} style={style} {...rest}>
      {children}
    </Space>
  );
};

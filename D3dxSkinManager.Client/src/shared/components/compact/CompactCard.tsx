import React from 'react';
import { Card, CardProps } from 'antd';

/**
 * CompactCard - A card component with reduced spacing for more compact layouts
 *
 * Features:
 * - Reduced margins and padding compared to default Ant Design Card
 * - Consistent spacing across the application
 * - Maintains all Ant Design Card props and functionality
 *
 * Usage:
 * <CompactCard title="My Title">Content</CompactCard>
 */

export interface CompactCardProps extends Omit<CardProps, 'style' | 'styles'> {
  /** Additional inline styles to apply to the card */
  style?: React.CSSProperties;
  /** Additional body styles to apply to the card body */
  bodyStyle?: React.CSSProperties;
  /** Whether to use extra compact spacing (reduces padding even more) */
  extraCompact?: boolean;
}

export const CompactCard: React.FC<CompactCardProps> = ({
  children,
  style,
  bodyStyle,
  extraCompact = false,
  ...rest
}) => {
  const defaultStyle: React.CSSProperties = {
    marginBottom: extraCompact ? '12px' : '16px',
    ...style,
  };

  const defaultBodyStyle: React.CSSProperties = {
    padding: extraCompact ? '12px' : '16px',
    ...bodyStyle,
  };

  return (
    <Card
      style={defaultStyle}
      styles={{ body: defaultBodyStyle }}
      {...rest}
    >
      {children}
    </Card>
  );
};

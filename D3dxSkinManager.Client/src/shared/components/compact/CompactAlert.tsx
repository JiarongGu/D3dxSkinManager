import React from 'react';
import { Alert, AlertProps } from 'antd';
import './CompactAlert.css';

/**
 * CompactAlert - An alert component with compact spacing and smaller icons
 *
 * Features:
 * - Reduced padding (8px 12px vs default 12px 15px)
 * - Smaller icon size (16px vs default 24px)
 * - Reduced margins for compact layouts
 * - Extra compact variant for very tight spaces
 * - Maintains all Ant Design Alert props and functionality
 *
 * Usage:
 * <CompactAlert type="info" title="Information" />
 * <CompactAlert type="warning" title="Warning" extraCompact />
 */

export interface CompactAlertProps extends Omit<AlertProps, 'style' | 'className'> {
  /** Additional inline styles to apply to the alert */
  style?: React.CSSProperties;
  /** Additional CSS class names */
  className?: string;
  /** Whether to use extra compact spacing (smaller padding and margins) */
  extraCompact?: boolean;
}

export const CompactAlert: React.FC<CompactAlertProps> = ({
  style,
  className = '',
  extraCompact = false,
  ...rest
}) => {
  // Build className with compact classes
  const alertClassName = `compact-alert ${extraCompact ? 'compact-alert-extra' : ''} ${className}`.trim();

  return <Alert className={alertClassName} style={style} {...rest} />;
};

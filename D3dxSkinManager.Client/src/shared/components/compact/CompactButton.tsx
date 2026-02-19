import React from 'react';
import { Button, ButtonProps } from 'antd';
import './CompactButton.css';

/**
 * CompactButton - A button component with consistent sizing and styling
 *
 * Features:
 * - Consistent sizing across the application (default: 'medium' - 32px)
 * - Enforced height and padding for visual consistency
 * - Maintains all Ant Design Button props and functionality
 * - Centralized styling management
 *
 * Usage:
 * <CompactButton type="primary">Click Me</CompactButton>
 * <CompactButton size="large" icon={<SaveOutlined />}>Save</CompactButton>
 *
 * Size Guide:
 * - small: 24px height - Compact buttons for tables, cards, inline actions
 * - medium: 32px height (Default) - Standard buttons for forms, modals, most UI actions
 * - large: 40px height - Prominent primary actions, CTAs
 */

export type CompactButtonSize = 'small' | 'medium' | 'large';

export interface CompactButtonProps extends Omit<ButtonProps, 'size'> {
  /** Button size - defaults to 'medium' (32px) for consistency */
  size?: CompactButtonSize;
  /** Additional CSS class names */
  className?: string;
}

export const CompactButton: React.FC<CompactButtonProps> = ({
  size = 'medium',
  className = '',
  children,
  ...rest
}) => {
  // Map our size to Ant Design size
  const antdSize = size === 'medium' ? 'middle' : size;

  // Build className with size-specific class
  const buttonClassName = `compact-button compact-button-${size} ${className}`.trim();

  return (
    <Button size={antdSize} className={buttonClassName} {...rest}>
      {children}
    </Button>
  );
};

/**
 * CompactButton.Primary - Primary action button (convenience wrapper)
 */
export const CompactPrimaryButton: React.FC<CompactButtonProps> = (props) => (
  <CompactButton type="primary" {...props} />
);

/**
 * CompactButton.Text - Text button (convenience wrapper)
 */
export const CompactTextButton: React.FC<CompactButtonProps> = (props) => (
  <CompactButton type="text" {...props} />
);

/**
 * CompactButton.Link - Link button (convenience wrapper)
 */
export const CompactLinkButton: React.FC<CompactButtonProps> = (props) => (
  <CompactButton type="link" {...props} />
);

/**
 * CompactButton.Danger - Danger button (convenience wrapper)
 */
export const CompactDangerButton: React.FC<CompactButtonProps> = (props) => (
  <CompactButton danger {...props} />
);

// Create a compound component pattern
const CompactButtonNamespace = Object.assign(CompactButton, {
  Primary: CompactPrimaryButton,
  Text: CompactTextButton,
  Link: CompactLinkButton,
  Danger: CompactDangerButton,
});

export default CompactButtonNamespace;

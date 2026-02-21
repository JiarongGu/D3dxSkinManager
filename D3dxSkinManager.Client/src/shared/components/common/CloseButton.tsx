import React from 'react';
import { CloseOutlined } from '@ant-design/icons';
import './CloseButton.css';

/**
 * CloseButton - Consistent close button with theme-aware styling
 *
 * Features:
 * - Matches fullscreen preview close button design
 * - Theme-aware (dark/light mode)
 * - 32x32 square with border
 * - Smooth hover transitions
 *
 * Usage:
 * <CloseButton onClick={handleClose} />
 * <CloseButton onClick={handleClose} size="large" />
 */

export interface CloseButtonProps {
  /** Click handler */
  onClick?: () => void;
  /** Size variant - defaults to 'medium' (32px) */
  size?: 'small' | 'medium' | 'large';
  /** Additional CSS class names */
  className?: string;
  /** Optional aria-label for accessibility */
  ariaLabel?: string;
}

export const CloseButton: React.FC<CloseButtonProps> = ({
  onClick,
  size = 'medium',
  className = '',
  ariaLabel = 'Close'
}) => {
  const buttonClassName = `close-button close-button-${size} ${className}`.trim();

  return (
    <div
      className={buttonClassName}
      onClick={onClick}
      role="button"
      tabIndex={0}
      aria-label={ariaLabel}
      onKeyDown={(e) => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault();
          onClick?.();
        }
      }}
    >
      <CloseOutlined />
    </div>
  );
};

export default CloseButton;

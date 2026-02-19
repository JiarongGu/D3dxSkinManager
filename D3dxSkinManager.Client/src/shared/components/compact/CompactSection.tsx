import React from 'react';
import { CompactTitle } from './CompactText';
import { CompactSpace } from './CompactSpace';

/**
 * CompactSection - A semantic section component with consistent compact styling
 *
 * Features:
 * - Optional title with consistent styling
 * - Automatic vertical spacing between children
 * - Full width by default
 *
 * Usage:
 * <CompactSection title="Section Title">
 *   <div>Content 1</div>
 *   <div>Content 2</div>
 * </CompactSection>
 */

export interface CompactSectionProps {
  /** Section title (optional) */
  title?: React.ReactNode;
  /** Title level (default: 4) */
  titleLevel?: 1 | 2 | 3 | 4 | 5;
  /** Section content */
  children: React.ReactNode;
  /** Additional class name */
  className?: string;
  /** Additional inline styles */
  style?: React.CSSProperties;
  /** Space size between children (default: 'small') */
  spacing?: 'small' | 'middle' | 'large' | number;
}

export const CompactSection: React.FC<CompactSectionProps> = ({
  title,
  titleLevel = 4,
  children,
  className,
  style,
  spacing = 'small',
}) => {
  return (
    <div className={className} style={style}>
      {title && (
        <CompactTitle level={titleLevel}>
          {title}
        </CompactTitle>
      )}
      <CompactSpace vertical style={{ width: '100%' }} size={spacing}>
        {children}
      </CompactSpace>
    </div>
  );
};

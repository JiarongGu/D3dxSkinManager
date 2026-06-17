import React from 'react';
import './CountBadge.css';

export interface CountBadgeProps {
  count: number;
  showZero?: boolean;
  className?: string;
}

export const CountBadge: React.FC<CountBadgeProps> = ({
  count,
  showZero = false,
  className,
}) => {
  // Guard against a non-numeric count (e.g. an unresolved/odd payload) — rendering a non-primitive
  // as a React child throws "Objects are not valid as a React child" and blanks the tree.
  const value = typeof count === 'number' && Number.isFinite(count) ? count : 0;
  if (value === 0 && !showZero) return null;

  return (
    <span className={className ? `count-badge ${className}` : 'count-badge'}>
      {value}
    </span>
  );
};

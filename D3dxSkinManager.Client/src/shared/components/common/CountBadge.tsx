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
  if (count === 0 && !showZero) return null;

  return (
    <span className={className ? `count-badge ${className}` : 'count-badge'}>
      {count}
    </span>
  );
};

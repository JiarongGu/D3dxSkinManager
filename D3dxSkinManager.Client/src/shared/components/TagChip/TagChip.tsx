import React from 'react';
import classNames from 'classnames';
import { Tag as AntTag } from 'antd';
import { Tag } from '../../types/mod.types';
import './TagChip.css';

export interface TagChipProps {
  /** Tag name to display */
  tagName: string;
  /** Optional: Override with Tag object if already loaded */
  tag?: Tag;
  /** Optional: Show close button */
  closable?: boolean;
  /** Optional: Close handler */
  onClose?: () => void;
  /** Optional: Click handler */
  onClick?: () => void;
  /** Optional: Additional class name */
  className?: string;
  /** Optional: Size */
  size?: 'small' | 'default' | 'large';
}

/**
 * TagChip Component
 * Displays a tag with its color from the Tags table (from the `tag` prop; falls back to the default
 * Ant Design style when no tag/color is supplied). Purely presentational — no async fetch.
 */
export const TagChip: React.FC<TagChipProps> = ({
  tagName,
  tag,
  closable = false,
  onClose,
  onClick,
  className,
  size = 'default',
}) => {
  return (
    <AntTag
      color={tag?.color || 'default'}
      closable={closable}
      onClose={onClose}
      onClick={onClick}
      className={classNames('tag-chip', {
        [`tag-chip-${size}`]: size !== 'default',
        'tag-chip-clickable': onClick
      }, className)}
    >
      {tagName}
    </AntTag>
  );
};

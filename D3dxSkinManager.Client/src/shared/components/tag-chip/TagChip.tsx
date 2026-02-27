import React, { useEffect, useState } from 'react';
import classNames from 'classnames';
import { Tag as AntTag } from 'antd';
import { modService } from '../../../modules/mods/services/modService';
import { Tag } from '../../types/mod.types';
import { useProfile } from '../../context/ProfileContext';
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
 * Displays a tag with its color from the Tags table
 * Auto-fetches tag metadata if not provided
 * Fallback to default color if tag not found
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
  const { state: profileState } = useProfile();
  const selectedProfileId = profileState.selectedProfile?.id;
  const [tagData, setTagData] = useState<Tag | undefined>(tag);
  const [loading, setLoading] = useState(!tag);

  // Use tag data if provided, otherwise use default style
  useEffect(() => {
    if (tag) {
      setTagData(tag);
    } else {
      // No tag data - will use default Ant Design tag style
      setTagData(undefined);
    }
    setLoading(false);
  }, [tagName, tag]);

  return (
    <AntTag
      color={tagData?.color || 'default'}
      closable={closable}
      onClose={onClose}
      onClick={onClick}
      className={classNames('tag-chip', {
        [`tag-chip-${size}`]: size !== 'default',
        'tag-chip-clickable': onClick
      }, className)}
    >
      {loading ? '...' : tagName}
    </AntTag>
  );
};

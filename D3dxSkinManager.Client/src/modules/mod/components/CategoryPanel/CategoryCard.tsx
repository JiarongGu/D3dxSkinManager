import React from 'react';
import { FolderOutlined, FileOutlined } from '@ant-design/icons';
import { CategoryInfo } from '../../../../shared/types/category.types';
import { toAppUrl } from '../../../../shared/utils/imageUrlHelper';
import { useTranslation } from 'react-i18next';
import classNames from 'classnames';
import './CategoryCard.css';

interface CategoryCardProps {
  category: CategoryInfo;
  isSelected: boolean;
  isParent?: boolean;
  isDropTarget?: boolean;
  onClick: () => void;
  onContextMenu: (e: React.MouseEvent) => void;
  onDragStart?: (e: React.DragEvent, nodeId: string) => void;
  onDragEnd?: () => void;
}

export const CategoryCard: React.FC<CategoryCardProps> = ({
  category,
  isSelected,
  isParent,
  isDropTarget,
  onClick,
  onContextMenu,
  onDragStart,
  onDragEnd,
}) => {
  const { t } = useTranslation();
  const hasChildren = category.children.length > 0;
  const hasThumbnail = !!category.thumbnail;

  return (
    <div
      data-node-id={category.id}
      className={classNames('category-card', {
        'category-card--selected': isSelected,
        'category-card--parent': isParent,
        'category-card--drop-target': isDropTarget,
      })}
      draggable
      onClick={onClick}
      onContextMenu={onContextMenu}
      onDragStart={(e) => {
        e.dataTransfer.setData('application/tree-node-id', category.id);
        e.dataTransfer.effectAllowed = 'move';
        onDragStart?.(e, category.id);
      }}
      onDragEnd={() => {
        onDragEnd?.();
      }}
    >
      <div className="category-card__thumbnail">
        {hasThumbnail && category.thumbnail ? (
          <img
            src={toAppUrl(category.thumbnail) || undefined}
            alt={category.name}
            className="category-card__image"
            loading="lazy"
          />
        ) : (
          <span className="category-card__icon">
            {hasChildren ? <FolderOutlined /> : <FileOutlined />}
          </span>
        )}
        {hasChildren && hasThumbnail && (
          <span className="category-card__folder-badge">
            <FolderOutlined />
          </span>
        )}
        {category.modCount !== undefined && category.modCount > 0 && (
          <span className="category-card__count">{category.modCount}</span>
        )}
      </div>
      <div className="category-card__name" title={category.name}>
        {category.name}
      </div>
      {/* "Move as child" overlay */}
      {isDropTarget && (
        <div className="category-card__drop-overlay">
          {t('category.moveAsChild')}
        </div>
      )}
    </div>
  );
};

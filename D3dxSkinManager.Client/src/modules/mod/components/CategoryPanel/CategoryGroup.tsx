import React from 'react';
import classNames from 'classnames';
import { CategoryInfo } from '../../../../shared/types/category.types';
import { ModInfo } from '../../../../shared/types/mod.types';
import { CategoryCard } from './CategoryCard';
import { activeModsForNode } from './TreeNodeConverter';
import { buildSegments, DropIndicator } from './categoryGridSegments';

/** Drop placeholder shown at a group's leading/trailing edge during a drag. */
const GroupPlaceholder: React.FC = () => (
  <div className="category-grid-drop-placeholder category-grid-drop-placeholder--group" />
);

export interface CategoryGroupProps {
  category: CategoryInfo;
  depth: number;
  selectedId: string | undefined;
  selectedCategoryIds: Set<string>;
  dropIndicator: DropIndicator | undefined;
  expandedKeys: React.Key[];
  lockedCategoriesSet: Set<string>;
  activeByCategory: Map<string, ModInfo[]>;
  onSelectCategory: (node: CategoryInfo, e: React.MouseEvent) => void;
  onDoubleClickCategory: (node: CategoryInfo) => void;
  onContextMenu: (e: React.MouseEvent, nodeId: string) => void;
  onCardDragStart: (e: React.DragEvent, nodeId: string) => void;
  onCardDragEnd: () => void;
  onLockCategory: (nodeId: string) => void;
  onUnlockCategory: (nodeId: string) => void;
}

/**
 * An expanded category group: the parent card + its child cards/sub-groups, laid out in original order
 * and recursing for nested expanded parents. Dumb/presentational — all data + callbacks flow in as props.
 * Extracted verbatim from CategoryGrid (behavior-preserving) so the grid component stays lean.
 */
export const CategoryGroup: React.FC<CategoryGroupProps> = ({
  category,
  depth,
  selectedId,
  selectedCategoryIds,
  dropIndicator,
  expandedKeys,
  lockedCategoriesSet,
  activeByCategory,
  onSelectCategory,
  onDoubleClickCategory,
  onContextMenu,
  onCardDragStart,
  onCardDragEnd,
  onLockCategory,
  onUnlockCategory,
}) => {
  // Build child segments in original order
  const childSegments = buildSegments(category.children, expandedKeys);

  // Group-level placeholders (before/after group box — triggered by group edge zones)
  const showPlaceholderBeforeGroup =
    dropIndicator?.targetNodeId === category.id &&
    dropIndicator.position === 'before';
  const showPlaceholderAfterGroup =
    dropIndicator?.targetNodeId === category.id &&
    dropIndicator.position === 'after';

  /** Render a card for a child node (leaf or collapsed parent) */
  const renderChildCard = (child: CategoryInfo) => {
    const hasChildren = child.children.length > 0;
    const moveIndicator =
      dropIndicator?.targetNodeId === child.id && dropIndicator.position === 'before' ? 'before' as const :
      dropIndicator?.targetNodeId === child.id && dropIndicator.position === 'after' ? 'after' as const :
      undefined;
    const isChildDropTarget =
      dropIndicator?.targetNodeId === child.id && dropIndicator.position === 'inside';
    return (
      <CategoryCard
        key={child.id}
        category={child}
        activeMods={activeModsForNode(child, activeByCategory, expandedKeys)}
        isSelected={selectedId === child.id}
        isMultiSelected={selectedCategoryIds.has(child.id)}
        isLocked={hasChildren ? lockedCategoriesSet.has(child.id) : undefined}
        isDropTarget={isChildDropTarget}
        moveIndicator={moveIndicator}
        onClick={(e) => onSelectCategory(child, e)}
        onDoubleClick={hasChildren ? () => onDoubleClickCategory(child) : undefined}
        onContextMenu={(e) => onContextMenu(e, child.id)}
        onDragStart={onCardDragStart}
        onDragEnd={onCardDragEnd}
        onLockClick={hasChildren ? onLockCategory : undefined}
        onUnlockClick={hasChildren ? onUnlockCategory : undefined}
      />
    );
  };

  return (
    <>
      {showPlaceholderBeforeGroup && <GroupPlaceholder />}
      <div
        className={classNames('category-grid-group', {
          'category-grid-group--nested': depth > 0,
          'category-grid-group--drop-target':
            dropIndicator?.targetNodeId === category.id && dropIndicator.position === 'inside',
        })}
        data-group-id={category.id}
      >
        {/* First cards row always starts with the parent card */}
        {(() => {
          // Find leading card-nodes to place in the same grid as the parent card
          const firstSegment = childSegments[0];
          const leadingCards = firstSegment?.type === 'cards' ? firstSegment.nodes : [];
          const startIdx = leadingCards.length > 0 ? 1 : 0; // skip first segment if consumed

          return (
            <>
              <div className="category-grid-cards">
                <CategoryCard
                  category={category}
                  activeMods={activeModsForNode(category, activeByCategory, expandedKeys)}
                  isSelected={selectedId === category.id}
                  isMultiSelected={selectedCategoryIds.has(category.id)}
                  isParent
                  isLocked={lockedCategoriesSet.has(category.id)}
                  isDropTarget={dropIndicator?.targetNodeId === category.id && dropIndicator.position === 'inside'}
                  onClick={(e) => onSelectCategory(category, e)}
                  onDoubleClick={() => onDoubleClickCategory(category)}
                  onContextMenu={(e) => onContextMenu(e, category.id)}
                  onDragStart={onCardDragStart}
                  onDragEnd={onCardDragEnd}
                  onLockClick={onLockCategory}
                  onUnlockClick={onUnlockCategory}
                />
                {leadingCards.map(renderChildCard)}
              </div>

              {/* Remaining segments in order */}
              {childSegments.slice(startIdx).map((seg, i) => {
                if (seg.type === 'group') {
                  return (
                    <CategoryGroup
                      key={seg.node.id}
                      category={seg.node}
                      depth={depth + 1}
                      selectedId={selectedId}
                      selectedCategoryIds={selectedCategoryIds}
                      dropIndicator={dropIndicator}
                      expandedKeys={expandedKeys}
                      lockedCategoriesSet={lockedCategoriesSet}
                      activeByCategory={activeByCategory}
                      onSelectCategory={onSelectCategory}
                      onDoubleClickCategory={onDoubleClickCategory}
                      onContextMenu={onContextMenu}
                      onCardDragStart={onCardDragStart}
                      onCardDragEnd={onCardDragEnd}
                      onLockCategory={onLockCategory}
                      onUnlockCategory={onUnlockCategory}
                    />
                  );
                }
                return (
                  <div key={`cards-${i}`} className="category-grid-cards">
                    {seg.nodes.map(renderChildCard)}
                  </div>
                );
              })}
            </>
          );
        })()}
      </div>
      {showPlaceholderAfterGroup && <GroupPlaceholder />}
    </>
  );
};

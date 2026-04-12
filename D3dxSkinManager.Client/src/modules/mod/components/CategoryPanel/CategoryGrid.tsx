import React, { useCallback, useState } from 'react';
import { Empty, Spin, Input, Button } from 'antd';
import { PlusOutlined } from '@ant-design/icons';
import { CategoryInfo } from '../../../../shared/types/category.types';
import { CategoryCard } from './CategoryCard';
import { useCategoryTreeContext } from './CategoryTreeContext';
import { ContextMenu, ContextMenuItem } from '../../../../shared/components/menu/ContextMenu';
import { useDragDrop } from '../../../../shared/hooks/useDragDrop';
import { useScrollPosition } from '../../../../shared/hooks/useScrollPosition';
import { useModsStore } from '../../store/modsStore';
import { logger } from '../../../../shared/utils/logger';
import { useTranslation } from 'react-i18next';
import type { MenuProps } from 'antd';
import classNames from 'classnames';
import './CategoryGrid.css';

const { Search } = Input;

// ============================================================
// Types
// ============================================================

interface DropIndicator {
  targetNodeId: string;
  position: 'before' | 'after' | 'inside';
  /** True when the target is a parent card and "after" means "before first child" */
  insertAfterParent?: boolean;
}

// ============================================================
// Helpers
// ============================================================

const extractNodeId = (target: Element | null): string => {
  if (!target) return '';
  let nodeId = (target as HTMLElement).getAttribute('data-node-id');
  if (nodeId) return nodeId;

  const elementWithId = (target as HTMLElement).querySelector('[data-node-id]');
  if (elementWithId) {
    nodeId = elementWithId.getAttribute('data-node-id');
    if (nodeId) return nodeId;
  }

  const parentWithId = (target as HTMLElement).closest('[data-node-id]');
  if (parentWithId) {
    nodeId = parentWithId.getAttribute('data-node-id');
    if (nodeId) return nodeId;
  }

  return '';
};

const convertMenuItems = (items: MenuProps['items']): ContextMenuItem[] => {
  if (!items) return [];
  return items
    .filter((item): item is NonNullable<typeof item> => item != null)
    .map((item) => {
      if ('type' in item && item.type === 'divider') {
        return { type: 'divider' as const };
      }
      const menuItem = item as {
        key?: string | number;
        label?: React.ReactNode;
        icon?: React.ReactNode;
        danger?: boolean;
        disabled?: boolean;
        onClick?: () => void;
      };
      return {
        key: String(menuItem.key || ''),
        label: String(menuItem.label || ''),
        icon: menuItem.icon,
        danger: menuItem.danger,
        disabled: menuItem.disabled,
        onClick: menuItem.onClick,
      };
    });
};

// ============================================================
// Drop Placeholder
// ============================================================

const CardPlaceholder: React.FC = () => (
  <div className="category-grid-drop-placeholder category-grid-drop-placeholder--card" />
);

const GroupPlaceholder: React.FC = () => (
  <div className="category-grid-drop-placeholder category-grid-drop-placeholder--group" />
);

// ============================================================
// CategoryGroup
// ============================================================

interface CategoryGroupProps {
  category: CategoryInfo;
  depth: number;
  selectedId: string | undefined;
  dropIndicator: DropIndicator | undefined;
  onSelectCategory: (node: CategoryInfo) => void;
  onContextMenu: (e: React.MouseEvent, nodeId: string) => void;
  onCardDragStart: (e: React.DragEvent, nodeId: string) => void;
  onCardDragEnd: () => void;
}

const CategoryGroup: React.FC<CategoryGroupProps> = ({
  category,
  depth,
  selectedId,
  dropIndicator,
  onSelectCategory,
  onContextMenu,
  onCardDragStart,
  onCardDragEnd,
}) => {
  const leafChildren = category.children.filter(c => c.children.length === 0);
  const parentChildren = category.children.filter(c => c.children.length > 0);

  // Group-level placeholders (before/after group box)
  const showPlaceholderBeforeGroup =
    dropIndicator?.targetNodeId === category.id &&
    dropIndicator.position === 'before' &&
    !dropIndicator.insertAfterParent;
  const showPlaceholderAfterGroup =
    dropIndicator?.targetNodeId === category.id &&
    dropIndicator.position === 'after' &&
    !dropIndicator.insertAfterParent;

  // "Insert before first child" placeholder (after parent card in the cards grid)
  const showInsertAfterParent =
    dropIndicator?.targetNodeId === category.id &&
    dropIndicator.insertAfterParent;

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
        <div className="category-grid-cards">
          <CategoryCard
            category={category}
            isSelected={selectedId === category.id}
            isParent
            isDropTarget={dropIndicator?.targetNodeId === category.id && dropIndicator.position === 'inside'}
            onClick={() => onSelectCategory(category)}
            onContextMenu={(e) => onContextMenu(e, category.id)}
            onDragStart={onCardDragStart}
            onDragEnd={onCardDragEnd}
          />
          {showInsertAfterParent && <CardPlaceholder />}
          {leafChildren.map(child => {
            const placeholderBefore =
              dropIndicator?.targetNodeId === child.id && dropIndicator.position === 'before';
            const placeholderAfter =
              dropIndicator?.targetNodeId === child.id && dropIndicator.position === 'after';
            const isChildDropTarget =
              dropIndicator?.targetNodeId === child.id && dropIndicator.position === 'inside';
            return (
              <React.Fragment key={child.id}>
                {placeholderBefore && <CardPlaceholder />}
                <CategoryCard
                  category={child}
                  isSelected={selectedId === child.id}
                  isDropTarget={isChildDropTarget}
                  onClick={() => onSelectCategory(child)}
                  onContextMenu={(e) => onContextMenu(e, child.id)}
                  onDragStart={onCardDragStart}
                  onDragEnd={onCardDragEnd}
                />
                {placeholderAfter && <CardPlaceholder />}
              </React.Fragment>
            );
          })}
        </div>

        {parentChildren.map(child => (
          <CategoryGroup
            key={child.id}
            category={child}
            depth={depth + 1}
            selectedId={selectedId}
            dropIndicator={dropIndicator}
            onSelectCategory={onSelectCategory}
            onContextMenu={onContextMenu}
            onCardDragStart={onCardDragStart}
            onCardDragEnd={onCardDragEnd}
          />
        ))}
      </div>
      {showPlaceholderAfterGroup && <GroupPlaceholder />}
    </>
  );
};

// ============================================================
// CategoryGrid
// ============================================================

export const CategoryGrid: React.FC = () => {
  const { t } = useTranslation();
  const {
    loading,
    tree,
    filteredTree,
    selectedNode,
    searchQuery,
    onSearchChange,
    onAddCategory,
    contextMenuNode,
    setContextMenuNode,
    contextMenuItems,
    contextMenuPosition,
    setContextMenuPosition,
    handleModClassify,
    handleBulkModClassify,
    handleNodeReorder,
    onSelect,
  } = useCategoryTreeContext();

  const { scrollRef, saveScrollPosition, restoreScrollPosition } = useScrollPosition('category-grid');

  React.useEffect(() => {
    if (loading) {
      saveScrollPosition();
    } else {
      restoreScrollPosition();
    }
  }, [loading, saveScrollPosition, restoreScrollPosition]);

  const handleContextMenu = useCallback(
    (e: React.MouseEvent, nodeId: string) => {
      e.preventDefault();
      setContextMenuPosition({ x: e.clientX, y: e.clientY });
      setContextMenuNode(nodeId);
    },
    [setContextMenuPosition, setContextMenuNode],
  );

  const handleContextMenuClose = useCallback(() => {
    setContextMenuNode(undefined);
    setContextMenuPosition({ x: 0, y: 0 });
  }, [setContextMenuNode, setContextMenuPosition]);

  const handleSelectCategory = useCallback(
    (node: CategoryInfo) => {
      useModsStore.getState().setSelectedCategory(node);
      onSelect(node);
    },
    [onSelect],
  );

  // ---- Drag state ----
  const draggedNodeKeyRef = React.useRef<string>(undefined);
  const dropIndicatorRef = React.useRef<DropIndicator | undefined>(undefined);
  const [dropIndicator, setDropIndicator] = useState<DropIndicator>();

  React.useEffect(() => {
    dropIndicatorRef.current = dropIndicator;
  }, [dropIndicator]);

  const handleCardDragStart = useCallback((_e: React.DragEvent, nodeId: string) => {
    draggedNodeKeyRef.current = nodeId;
  }, []);

  const clearDragState = useCallback(() => {
    draggedNodeKeyRef.current = undefined;
    setDropIndicator(undefined);
    dropIndicatorRef.current = undefined;
  }, []);

  // ---- X-axis drag position tracking ----
  const handleGridDragOver = useCallback((e: React.DragEvent) => {
    if (!draggedNodeKeyRef.current) return;
    e.preventDefault();

    const cardTarget = (e.target as HTMLElement).closest('[data-node-id]') as HTMLElement | null;

    if (cardTarget) {
      // Card-level detection: X-axis (left/center/right)
      const nodeId = cardTarget.getAttribute('data-node-id')!;
      if (nodeId === draggedNodeKeyRef.current) {
        setDropIndicator(undefined);
        return;
      }

      const rect = cardTarget.getBoundingClientRect();
      const relativeX = (e.clientX - rect.left) / rect.width;
      const edgeThreshold = 0.3;
      const isParentCard = cardTarget.classList.contains('category-card--parent');

      let position: 'before' | 'after' | 'inside';
      let insertAfterParent = false;

      if (relativeX < edgeThreshold) {
        position = 'before';
      } else if (relativeX > 1 - edgeThreshold) {
        position = 'after';
        // Parent card right edge = "insert before first child in this group"
        if (isParentCard) {
          insertAfterParent = true;
        }
      } else {
        position = 'inside';
      }

      setDropIndicator(prev => {
        if (prev?.targetNodeId === nodeId && prev?.position === position && prev?.insertAfterParent === insertAfterParent) return prev;
        return { targetNodeId: nodeId, position, insertAfterParent };
      });
    } else {
      // Not over any card — check if near the top/bottom edge of a group box (for group reorder)
      const groupTarget = (e.target as HTMLElement).closest('[data-group-id]') as HTMLElement | null;
      if (groupTarget) {
        const groupId = groupTarget.getAttribute('data-group-id')!;
        if (groupId === draggedNodeKeyRef.current) return;

        // Only trigger group reorder when cursor is near the very edge of the group box,
        // not when hovering the interior gaps between cards
        const rect = groupTarget.getBoundingClientRect();
        const distFromTop = e.clientY - rect.top;
        const distFromBottom = rect.bottom - e.clientY;
        const edgePx = 10;

        if (distFromTop < edgePx) {
          setDropIndicator(prev => {
            if (prev?.targetNodeId === groupId && prev?.position === 'before' && !prev?.insertAfterParent) return prev;
            return { targetNodeId: groupId, position: 'before' };
          });
        } else if (distFromBottom < edgePx) {
          setDropIndicator(prev => {
            if (prev?.targetNodeId === groupId && prev?.position === 'after' && !prev?.insertAfterParent) return prev;
            return { targetNodeId: groupId, position: 'after' };
          });
        }
        // Interior of group — keep last indicator, don't show group-level placeholder
      }
    }
  }, []);

  const handleGridDragLeave = useCallback((e: React.DragEvent) => {
    const relatedTarget = e.relatedTarget as HTMLElement | null;
    if (!relatedTarget || !e.currentTarget.contains(relatedTarget)) {
      setDropIndicator(undefined);
    }
  }, []);

  // ---- Drop handler ----
  const handleGridDrop = useCallback((e: React.DragEvent) => {
    if (!draggedNodeKeyRef.current) return;
    if (!e.dataTransfer.types.includes('application/tree-node-id')) return;
    e.preventDefault();

    const indicator = dropIndicatorRef.current;
    if (!indicator) {
      clearDragState();
      return;
    }

    const type = indicator.position === 'inside' ? 'node' as const : 'gap' as const;
    const gapPosition = indicator.position === 'before' ? 'top' as const : 'bottom' as const;

    logger.debug('[GridDrop] Reorder:', {
      drag: draggedNodeKeyRef.current,
      drop: indicator.targetNodeId,
      type,
      gapPosition: type === 'gap' ? gapPosition : undefined,
    });

    handleNodeReorder(
      draggedNodeKeyRef.current,
      indicator.targetNodeId,
      type,
      type === 'gap' ? gapPosition : undefined,
    );

    clearDragState();
  }, [handleNodeReorder, clearDragState]);

  // ---- useDragDrop: mod drops only ----
  const { containerRef: gridContainerRef } = useDragDrop<HTMLDivElement>(
    {
      eventType: 'application/mod-id',
      nodeSelector: '[data-node-id]',
      allow: 'node',
      onDrop: ({ data, target }) => {
        if (!data || !target) return false;
        const nodeId = extractNodeId(target);
        logger.debug('[GridModDrop] Dropping mod:', data, 'onto node:', nodeId);
        handleModClassify(data, nodeId);
        return true;
      },
    },
    {
      eventType: 'application/mod-ids',
      nodeSelector: '[data-node-id]',
      allow: 'node',
      onDrop: ({ data, target }) => {
        if (!data || !target) return false;
        try {
          const modIds = JSON.parse(data) as string[];
          const nodeId = extractNodeId(target);
          logger.debug('[GridBulkModDrop] Dropping mods:', modIds, 'onto node:', nodeId);
          handleBulkModClassify(modIds, nodeId);
          return true;
        } catch (error) {
          logger.error('[GridBulkModDrop] Failed to parse mod IDs:', error);
          return false;
        }
      },
    },
  );

  // ---- Render ----

  if (tree.length === 0 && !loading) {
    return (
      <div
        className="category-grid-empty-container"
        onContextMenu={(e) => handleContextMenu(e, '')}
      >
        <Empty
          description={t('category.tree.empty')}
          image={Empty.PRESENTED_IMAGE_SIMPLE}
        />
        <ContextMenu
          items={convertMenuItems(contextMenuItems)}
          visible={contextMenuNode !== undefined}
          position={contextMenuPosition}
          onClose={handleContextMenuClose}
        />
      </div>
    );
  }

  const rootLeaves = filteredTree.filter(n => n.children.length === 0);
  const rootParents = filteredTree.filter(n => n.children.length > 0);

  return (
    <div className="category-grid-container">
      <div className="category-grid-header">
        <Search
          placeholder={t('category.tree.searchPlaceholder')}
          value={searchQuery}
          onChange={(e) => onSearchChange(e.target.value)}
          className="category-grid-search"
          allowClear
        />
        <Button
          type="default"
          icon={<PlusOutlined />}
          onClick={() => onAddCategory?.()}
        />
      </div>

      <div className="category-grid-content-wrapper">
        {loading && (
          <div className="category-grid-loading-overlay">
            <Spin size="large" />
          </div>
        )}

        <div
          ref={(el) => {
            gridContainerRef(el || undefined);
            if (el) {
              scrollRef.current = el;
            }
          }}
          className={classNames('category-grid-scroll-container', {
            'category-grid-content-loading': loading,
          })}
          onDragOver={handleGridDragOver}
          onDragLeave={handleGridDragLeave}
          onDrop={handleGridDrop}
          onContextMenu={(e) => {
            const target = e.target as HTMLElement;
            if (!target.closest('[data-node-id]')) {
              e.stopPropagation();
              handleContextMenu(e, '');
            }
          }}
        >
          <div className="category-grid-inner">
            {rootLeaves.length > 0 && (
              <div className="category-grid-cards category-grid-root-cards">
                {rootLeaves.map(node => {
                  const placeholderBefore =
                    dropIndicator?.targetNodeId === node.id && dropIndicator.position === 'before';
                  const placeholderAfter =
                    dropIndicator?.targetNodeId === node.id && dropIndicator.position === 'after';
                  const isDropTarget =
                    dropIndicator?.targetNodeId === node.id && dropIndicator.position === 'inside';
                  return (
                    <React.Fragment key={node.id}>
                      {placeholderBefore && <CardPlaceholder />}
                      <CategoryCard
                        category={node}
                        isSelected={selectedNode?.id === node.id}
                        isDropTarget={isDropTarget}
                        onClick={() => handleSelectCategory(node)}
                        onContextMenu={(e) => handleContextMenu(e, node.id)}
                        onDragStart={handleCardDragStart}
                        onDragEnd={clearDragState}
                      />
                      {placeholderAfter && <CardPlaceholder />}
                    </React.Fragment>
                  );
                })}
              </div>
            )}

            {rootParents.map(node => (
              <CategoryGroup
                key={node.id}
                category={node}
                depth={0}
                selectedId={selectedNode?.id}
                dropIndicator={dropIndicator}
                onSelectCategory={handleSelectCategory}
                onContextMenu={handleContextMenu}
                onCardDragStart={handleCardDragStart}
                onCardDragEnd={clearDragState}
              />
            ))}
          </div>

          <ContextMenu
            items={convertMenuItems(contextMenuItems)}
            visible={contextMenuNode !== undefined}
            position={contextMenuPosition}
            onClose={handleContextMenuClose}
          />
        </div>
      </div>
    </div>
  );
};

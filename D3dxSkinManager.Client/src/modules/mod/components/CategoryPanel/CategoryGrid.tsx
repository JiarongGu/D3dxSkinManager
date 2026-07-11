import React, { useCallback, useMemo, useState } from 'react';
import { Empty, Spin } from 'antd';
import { PlusOutlined, FolderOpenOutlined, FolderOutlined } from '@ant-design/icons';
import { CategoryInfo } from '../../../../shared/types/category.types';
import { flattenCategoryTree } from '../../../../shared/utils/categoryTree';
import { ModInfo } from '../../../../shared/types/mod.types';
import { CategoryCard } from './CategoryCard';
import { groupModsByCategory, activeModsForNode } from './TreeNodeConverter';
import { useCategoryTreeContext } from './CategoryTreeContext';
import { ContextMenu } from '../../../../shared/components/menu/ContextMenu';
import { convertMenuItems } from '../../../../shared/components/menu/convertMenuItems';
import { useDragDrop } from '../../../../shared/hooks/useDragDrop';
import { useScrollPosition } from '../../../../shared/hooks/useScrollPosition';
import { useModsStore } from '../../store/modsStore';
import { useModsState } from '../../hooks/useMods';
import { logger } from '../../../../shared/utils/logger';
import { useTranslation } from 'react-i18next';
import type { MenuProps } from 'antd';
import classNames from 'classnames';
import './CategoryGrid.css';
import { CompactButton } from '../../../../shared/components/compact';
import { SearchToolbar } from '../../../../shared/components/common';

// ============================================================
// Types & Helpers
// ============================================================

interface DropIndicator {
  targetNodeId: string;
  position: 'before' | 'after' | 'inside';
}

/** An ordered segment: a run of card-nodes or a single expanded group */
type GridSegment =
  | { type: 'cards'; nodes: CategoryInfo[] }
  | { type: 'group'; node: CategoryInfo };

/**
 * Split an ordered list of nodes into segments that preserve the original order.
 * Leaves and collapsed parents are batched into consecutive "cards" segments;
 * expanded parents break out as individual "group" segments.
 */
const buildSegments = (
  nodes: CategoryInfo[],
  expandedKeys: React.Key[],
): GridSegment[] => {
  const segments: GridSegment[] = [];
  let currentCards: CategoryInfo[] = [];

  for (const node of nodes) {
    const isExpandedParent = node.children.length > 0 && expandedKeys.includes(node.id);
    if (isExpandedParent) {
      if (currentCards.length > 0) {
        segments.push({ type: 'cards', nodes: currentCards });
        currentCards = [];
      }
      segments.push({ type: 'group', node });
    } else {
      currentCards.push(node);
    }
  }
  if (currentCards.length > 0) {
    segments.push({ type: 'cards', nodes: currentCards });
  }
  return segments;
};

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


// ============================================================
// Drop Placeholder
// ============================================================

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

const CategoryGroup: React.FC<CategoryGroupProps> = ({
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
    expandedKeys,
    contextMenuNode,
    setContextMenuNode,
    contextMenuItems,
    contextMenuPosition,
    setContextMenuPosition,
    handleModClassify,
    handleBulkModClassify,
    handleNodeReorder,
    handleBatchMoveToParent,
    lockedCategoriesSet,
    handleLockExpanded,
    handleUnlockExpanded,
    onSelect,
    findNodeById,
  } = useCategoryTreeContext();

  // Multi-select state
  const selectedCategoryIds = useModsState(s => s.selectedCategoryIds);
  const selectedCategoryIdsSet = useMemo(() => new Set(selectedCategoryIds), [selectedCategoryIds]);

  // Active/loaded mods grouped by category id → drives the per-category active indicator.
  const activeMods = useModsState(s => s.activeMods);
  const activeByCategory = useMemo(() => groupModsByCategory(activeMods), [activeMods]);
  const anchorIdRef = React.useRef<string | undefined>(undefined);

  // Flat list of visible nodes for shift+click range selection
  const flatNodes = useMemo(() => flattenCategoryTree(filteredTree), [filteredTree]);

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

  // Grid-specific context menu: adds expand/collapse for parent nodes (uses lock logic)
  const gridContextMenuItems = useMemo(() => {
    if (!contextMenuNode || contextMenuNode === '') return contextMenuItems;

    const node = findNodeById(contextMenuNode);
    if (!node || node.children.length === 0) return contextMenuItems;

    const isLocked = lockedCategoriesSet.has(node.id);
    const expandCollapseItem = {
      key: 'toggle-expand',
      label: isLocked ? t('category.collapse') : t('category.expand'),
      icon: isLocked ? <FolderOutlined /> : <FolderOpenOutlined />,
      onClick: () => isLocked ? handleUnlockExpanded(node.id) : handleLockExpanded(node.id),
    };

    return [expandCollapseItem, { key: 'divider-expand', type: 'divider' as const }, ...(contextMenuItems || [])];
  }, [contextMenuNode, contextMenuItems, lockedCategoriesSet, findNodeById, handleLockExpanded, handleUnlockExpanded, t]);

  const handleSelectCategory = useCallback(
    (node: CategoryInfo, e: React.MouseEvent) => {
      const store = useModsStore.getState();

      if (e.ctrlKey || e.metaKey) {
        // Ctrl+click: toggle this item in multi-selection
        let current = store.selectedCategoryIds;
        // Bootstrap: if no multi-selection yet, seed with current single-selected item
        if (current.length === 0 && store.selectedCategory) {
          current = [store.selectedCategory.id];
        }
        const isAlreadySelected = current.includes(node.id);
        if (isAlreadySelected) {
          store.setSelectedCategoryIds(current.filter(id => id !== node.id));
        } else {
          store.setSelectedCategoryIds([...current, node.id]);
        }
        anchorIdRef.current = node.id;
      } else if (e.shiftKey) {
        // Shift+click: range select from anchor to this node
        // Fall back to current single-selected item if no anchor yet
        const anchorId = anchorIdRef.current || store.selectedCategory?.id;
        if (anchorId) {
          const anchorIdx = flatNodes.findIndex(n => n.id === anchorId);
          const targetIdx = flatNodes.findIndex(n => n.id === node.id);
          if (anchorIdx !== -1 && targetIdx !== -1) {
            const start = Math.min(anchorIdx, targetIdx);
            const end = Math.max(anchorIdx, targetIdx);
            const rangeIds = flatNodes.slice(start, end + 1).map(n => n.id);
            store.setSelectedCategoryIds(rangeIds);
          }
        }
      } else {
        // Plain click: single select, clear multi-selection
        store.setSelectedCategoryIds([]);
        anchorIdRef.current = node.id;
      }

      // Always set the primary selected category (loads mods in the list)
      store.setSelectedCategory(node);
      onSelect(node);
    },
    [onSelect, flatNodes],
  );

  // Double-click: lock (expand) / unlock (collapse) parent nodes
  const handleDoubleClickCategory = useCallback(
    (node: CategoryInfo) => {
      if (node.children.length === 0) return;
      if (lockedCategoriesSet.has(node.id)) {
        handleUnlockExpanded(node.id);
      } else {
        handleLockExpanded(node.id);
      }
    },
    [lockedCategoriesSet, handleLockExpanded, handleUnlockExpanded],
  );

  // ---- Drag state ----
  const draggedNodeKeyRef = React.useRef<string>(undefined);
  const draggedNodeKeysRef = React.useRef<string[]>([]);
  const dropIndicatorRef = React.useRef<DropIndicator | undefined>(undefined);
  const [dropIndicator, setDropIndicator] = useState<DropIndicator>();

  React.useEffect(() => {
    dropIndicatorRef.current = dropIndicator;
  }, [dropIndicator]);

  const handleCardDragStart = useCallback((_e: React.DragEvent, nodeId: string) => {
    const store = useModsStore.getState();
    const multiSelected = store.selectedCategoryIds;

    draggedNodeKeyRef.current = nodeId;

    if (multiSelected.length > 1 && multiSelected.includes(nodeId)) {
      // Dragging from within a multi-selection: drag all selected
      draggedNodeKeysRef.current = multiSelected;
    } else {
      // Dragging a single (non-multi-selected) card
      draggedNodeKeysRef.current = [nodeId];
    }
  }, []);

  const clearDragState = useCallback(() => {
    draggedNodeKeyRef.current = undefined;
    draggedNodeKeysRef.current = [];
    setDropIndicator(undefined);
    dropIndicatorRef.current = undefined;
  }, []);

  // ---- X-axis drag position tracking ----
  const handleGridDragOver = useCallback((e: React.DragEvent) => {
    if (!draggedNodeKeyRef.current) return;
    e.preventDefault();

    const cardTarget = (e.target as HTMLElement).closest('[data-node-id]') as HTMLElement | null;

    if (cardTarget) {
      const nodeId = cardTarget.getAttribute('data-node-id')!;
      // Skip indicator when hovering over any of the dragged nodes
      if (draggedNodeKeysRef.current.includes(nodeId)) {
        setDropIndicator(undefined);
        return;
      }

      const isMultiDrag = draggedNodeKeysRef.current.length > 1;
      const rect = cardTarget.getBoundingClientRect();
      const relativeX = (e.clientX - rect.left) / rect.width;
      const hasChildren = cardTarget.hasAttribute('data-has-children');
      // Parent/folder cards: large center zone for "drop into"; leaf cards: larger edges for reorder
      const edgeThreshold = hasChildren ? 0.15 : 0.3;
      const isParentCard = cardTarget.classList.contains('category-card--parent');

      let position: 'before' | 'after' | 'inside';

      if (isMultiDrag || isParentCard) {
        // Multi-drag or parent card: only "drop into" allowed
        // Group edge zones (top/bottom 10px of group box) handle before/after
        position = 'inside';
      } else if (relativeX < edgeThreshold) {
        position = 'before';
      } else if (relativeX > 1 - edgeThreshold) {
        position = 'after';
      } else {
        position = 'inside';
      }

      setDropIndicator(prev => {
        if (prev?.targetNodeId === nodeId && prev?.position === position) return prev;
        return { targetNodeId: nodeId, position };
      });
    } else {
      const groupTarget = (e.target as HTMLElement).closest('[data-group-id]') as HTMLElement | null;
      if (groupTarget) {
        const groupId = groupTarget.getAttribute('data-group-id')!;
        if (draggedNodeKeysRef.current.includes(groupId)) return;
        // Multi-drag: skip group edge zones (only card "inside" drops allowed)
        if (draggedNodeKeysRef.current.length > 1) return;

        const rect = groupTarget.getBoundingClientRect();
        const distFromTop = e.clientY - rect.top;
        const distFromBottom = rect.bottom - e.clientY;
        const edgePx = 10;

        if (distFromTop < edgePx) {
          setDropIndicator(prev => {
            if (prev?.targetNodeId === groupId && prev?.position === 'before') return prev;
            return { targetNodeId: groupId, position: 'before' };
          });
        } else if (distFromBottom < edgePx) {
          setDropIndicator(prev => {
            if (prev?.targetNodeId === groupId && prev?.position === 'after') return prev;
            return { targetNodeId: groupId, position: 'after' };
          });
        }
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

    const dragKeys = draggedNodeKeysRef.current;
    const isMultiDrag = dragKeys.length > 1;

    if (isMultiDrag && indicator.position === 'inside') {
      // Multi-drag into a parent: batch move
      logger.debug('[GridDrop] Batch move:', {
        dragIds: dragKeys,
        targetParent: indicator.targetNodeId,
      });
      handleBatchMoveToParent(dragKeys, indicator.targetNodeId);
      useModsStore.getState().setSelectedCategoryIds([]);
    } else {
      // Single drag (reorder or move into parent)
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
    }

    clearDragState();
  }, [handleNodeReorder, handleBatchMoveToParent, clearDragState]);

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
          items={convertMenuItems(gridContextMenuItems)}
          visible={contextMenuNode !== undefined}
          position={contextMenuPosition}
          onClose={handleContextMenuClose}
        />
      </div>
    );
  }

  const rootSegments = buildSegments(filteredTree, expandedKeys);

  /** Render a root-level card (leaf or collapsed parent — no parent styling) */
  const renderRootCard = (node: CategoryInfo) => {
    const hasChildren = node.children.length > 0;
    const moveIndicator =
      dropIndicator?.targetNodeId === node.id && dropIndicator.position === 'before' ? 'before' as const :
      dropIndicator?.targetNodeId === node.id && dropIndicator.position === 'after' ? 'after' as const :
      undefined;
    const isDropTarget =
      dropIndicator?.targetNodeId === node.id && dropIndicator.position === 'inside';
    return (
      <CategoryCard
        key={node.id}
        category={node}
        activeMods={activeModsForNode(node, activeByCategory, expandedKeys)}
        isSelected={selectedNode?.id === node.id}
        isMultiSelected={selectedCategoryIdsSet.has(node.id)}
        isLocked={hasChildren ? lockedCategoriesSet.has(node.id) : undefined}
        isDropTarget={isDropTarget}
        moveIndicator={moveIndicator}
        onClick={(e) => handleSelectCategory(node, e)}
        onDoubleClick={hasChildren ? () => handleDoubleClickCategory(node) : undefined}
        onContextMenu={(e) => handleContextMenu(e, node.id)}
        onDragStart={handleCardDragStart}
        onDragEnd={clearDragState}
        onLockClick={handleLockExpanded}
        onUnlockClick={handleUnlockExpanded}
      />
    );
  };

  return (
    <div className="category-grid-container">
      <SearchToolbar
        className="category-grid-header"
        inputClassName="category-grid-search"
        placeholder={t('category.tree.searchPlaceholder')}
        value={searchQuery}
        onChange={onSearchChange}
        action={
          <CompactButton
            type="default"
            icon={<PlusOutlined />}
            onClick={() => onAddCategory?.()}
          />
        }
      />

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
          onClick={(e) => {
            // Click on empty space: clear multi-selection
            const target = e.target as HTMLElement;
            if (!target.closest('[data-node-id]')) {
              useModsStore.getState().setSelectedCategoryIds([]);
            }
          }}
          onContextMenu={(e) => {
            const target = e.target as HTMLElement;
            if (!target.closest('[data-node-id]')) {
              e.stopPropagation();
              handleContextMenu(e, '');
            }
          }}
        >
          <div className="category-grid-inner">
            {rootSegments.map((seg, i) => {
              if (seg.type === 'cards') {
                return (
                  <div key={`root-cards-${i}`} className="category-grid-cards category-grid-root-cards">
                    {seg.nodes.map(renderRootCard)}
                  </div>
                );
              }
              return (
                <CategoryGroup
                  key={seg.node.id}
                  category={seg.node}
                  depth={0}
                  selectedId={selectedNode?.id}
                  selectedCategoryIds={selectedCategoryIdsSet}
                  dropIndicator={dropIndicator}
                  expandedKeys={expandedKeys}
                  lockedCategoriesSet={lockedCategoriesSet}
                  activeByCategory={activeByCategory}
                  onSelectCategory={handleSelectCategory}
                  onDoubleClickCategory={handleDoubleClickCategory}
                  onContextMenu={handleContextMenu}
                  onCardDragStart={handleCardDragStart}
                  onCardDragEnd={clearDragState}
                  onLockCategory={handleLockExpanded}
                  onUnlockCategory={handleUnlockExpanded}
                />
              );
            })}
          </div>

          <ContextMenu
            items={convertMenuItems(gridContextMenuItems)}
            visible={contextMenuNode !== undefined}
            position={contextMenuPosition}
            onClose={handleContextMenuClose}
          />
        </div>
      </div>
    </div>
  );
};

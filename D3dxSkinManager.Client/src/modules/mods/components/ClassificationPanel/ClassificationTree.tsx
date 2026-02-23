import React from "react";
import { Tree, Empty, Spin, Input, Button, Tooltip } from "antd";
import { PlusOutlined } from "@ant-design/icons";
import { ClassificationNode } from "../../../../shared/types/classification.types";
import {
  ClassificationTreeProvider,
  useClassificationTreeContext,
} from "./ClassificationTreeContext";
import { ContextMenu, ContextMenuItem } from "../../../../shared/components/menu/ContextMenu";
import { useDragDrop } from "../../../../shared/hooks/useDragDrop";
import { logger } from "../../../../shared/utils/logger";
import { useTranslation } from "react-i18next";
import type { MenuProps } from "antd";
import "./ClassificationTree.css";

const { Search } = Input;

/**
 * Extract node ID from tree element
 * Looks for data-node-id attribute that we set in the title span
 */
const extractNodeId = (target: Element | null): string => {
  if (!target) return "";

  // First check if the target itself has the attribute
  let nodeId = (target as HTMLElement).getAttribute('data-node-id');
  if (nodeId) {
    return nodeId;
  }

  // Check if target has a child with the attribute (the title span is inside the wrapper)
  const elementWithId = (target as HTMLElement).querySelector('[data-node-id]');
  if (elementWithId) {
    nodeId = elementWithId.getAttribute('data-node-id');
    if (nodeId) {
      return nodeId;
    }
  }

  // Also check parents in case target is inside the title span
  const parentWithId = (target as HTMLElement).closest('[data-node-id]');
  if (parentWithId) {
    nodeId = parentWithId.getAttribute('data-node-id');
    if (nodeId) {
      return nodeId;
    }
  }

  // Fallback: extract from text content (should not happen anymore)
  const textContent = target.textContent?.trim().replace(/\s*\(\d+\)$/, "") || "";
  return textContent;
};

/**
 * Convert Ant Design MenuProps items to ContextMenuItem array
 */
const convertMenuItems = (items: MenuProps['items']): ContextMenuItem[] => {
  if (!items) return [];
  return items
    .filter((item): item is NonNullable<typeof item> => item != null)
    .map(item => {
      // Handle divider type
      if ('type' in item && item.type === 'divider') {
        return { type: 'divider' as const };
      }
      // Handle regular menu items - Ant Design's ItemType has these properties
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

/**
 * Props for the main ClassificationTree component
 */
export interface ClassificationTreeProps {
  /**
   * Classification tree data from backend
   */
  tree: ClassificationNode[];

  /**
   * Loading state
   */
  loading: boolean;

  /**
   * Selected classification node
   */
  selectedNode: ClassificationNode | undefined;

  /**
   * Callback when a node is selected
   */
  onSelect: (node: ClassificationNode | undefined) => void;

  /**
   * Search query for filtering tree
   */
  searchQuery: string;

  /**
   * Callback when search query changes
   */
  onSearchChange: (query: string) => void;

  /**
   * Expanded keys for tree
   */
  expandedKeys: React.Key[];

  /**
   * Callback when expanded keys change
   */
  onExpandedKeysChange: (keys: React.Key[]) => void;

  /**
   * Callback when creating new classification
   */
  onAddClassification?: (parentId?: string) => void;

  /**
   * Callback to refresh tree after operations
   */
  onRefreshTree?: () => Promise<void>;

  /**
   * Callback to refresh mods after category update
   */
  onModsRefresh?: () => Promise<void>;
}

/**
 * Shared context menu component
 */
interface TreeContextMenuProps {
  items: MenuProps['items'];
  visible: boolean;
  position: { x: number; y: number };
  onClose: () => void;
}

const TreeContextMenu: React.FC<TreeContextMenuProps> = ({ items, visible, position, onClose }) => (
  <ContextMenu
    items={convertMenuItems(items)}
    visible={visible}
    position={position}
    onClose={onClose}
  />
);

/**
 * Internal tree component that uses the context
 */
const ClassificationTreeInner: React.FC = () => {
  const { t } = useTranslation();
  const {
    loading,
    tree,
    selectedNode,
    searchQuery,
    onSearchChange,
    onAddClassification,
    contextMenuNode,
    setContextMenuNode,
    contextMenuItems,
    contextMenuPosition,
    setContextMenuPosition,
    treeData,
    expandedKeys,
    handleSelect,
    handleRightClick,
    handleNodeReorder,
    handleModClassify,
  } = useClassificationTreeContext();

  // Track which node is being dragged
  const draggedNodeKeyRef = React.useRef<string>();

  // Shared context menu handler
  const handleContextMenu = React.useCallback((e: React.MouseEvent, nodeId: string | undefined) => {
    e.preventDefault();
    setContextMenuPosition({ x: e.clientX, y: e.clientY });
    setContextMenuNode(nodeId);
  }, [setContextMenuPosition, setContextMenuNode]);

  // Shared context menu close handler
  const handleContextMenuClose = React.useCallback(() => {
    setContextMenuNode(undefined);
    setContextMenuPosition({ x: 0, y: 0 });
  }, [setContextMenuNode, setContextMenuPosition]);

  // Enhanced drag and drop with simplified API
  const { containerRef: treeContainerRef } = useDragDrop<HTMLDivElement>(
    // Handler 1: Mod drops from the mod list (only allow dropping into nodes)
    {
      eventType: "application/mod-sha",
      nodeSelector: ".ant-tree-node-content-wrapper",
      allow: "node", // Only allow dropping into categories, not between them
      onDrop: ({ data, target }) => {
        if (!data) {
          logger.error('[ModDrop] No mod SHA provided');
          return false;
        }

        if (!target) {
          logger.error('[ModDrop] No target element');
          return false;
        }

        const nodeId = extractNodeId(target);
        logger.debug('[ModDrop] Dropping mod:', data, 'onto node:', nodeId);

        handleModClassify(data, nodeId);
        return true;
      },
    },
    // Handler 2: Tree node reorganization with automatic drop zones
    {
      eventType: "application/tree-node-id",
      nodeSelector: ".ant-tree-node-content-wrapper",
      allow: "all",
      gapThreshold: 0.15,
      onDrop: ({ data, type, gapPosition, target }) => {
        logger.debug('[TreeDrop] onDrop called:', { data, type, gapPosition, target, draggedNode: draggedNodeKeyRef.current });

        if (!data || !draggedNodeKeyRef.current) {
          logger.error('[TreeDrop] Missing data or draggedNodeKeyRef:', { data, draggedNode: draggedNodeKeyRef.current });
          return false;
        }

        if (!target) {
          logger.error('[TreeDrop] No target element');
          return false;
        }

        const dropNodeId = extractNodeId(target);

        logger.debug('[TreeDrop] Calling handleNodeReorder with:', {
          dragNode: draggedNodeKeyRef.current,
          dropNode: dropNodeId,
          dropType: type,
          gapSide: gapPosition,
          targetElement: target,
          targetText: target.textContent
        });

        handleNodeReorder(
          draggedNodeKeyRef.current,
          dropNodeId,
          type,
          gapPosition
        );

        logger.debug('[TreeDrop] handleNodeReorder called, returning true');
        return true;
      },
    },
  );

  if (loading) {
    return (
      <div className="classification-tree-loading-container">
        <Spin>
          <div className="classification-tree-loading-text">{t('classification.tree.loading')}</div>
        </Spin>
      </div>
    );
  }

  if (tree.length === 0) {
    return (
      <>
        <div
          className="classification-tree-empty-container"
          onContextMenu={(e) => handleContextMenu(e, "")}
        >
          <Empty
            description={t('classification.tree.empty')}
            image={Empty.PRESENTED_IMAGE_SIMPLE}
          />
        </div>
        <TreeContextMenu
          items={contextMenuItems}
          visible={contextMenuNode !== undefined}
          position={contextMenuPosition}
          onClose={handleContextMenuClose}
        />
      </>
    );
  }

  return (
    <div className="classification-tree-container">
      <div className="classification-tree-header">
        <Search
          placeholder={t('classification.tree.searchPlaceholder')}
          value={searchQuery}
          onChange={(e) => onSearchChange(e.target.value)}
          className="classification-tree-search"
          allowClear
        />
        <Tooltip title={t('classification.tree.addClassification')} placement="top">
          <Button
            type="default"
            icon={<PlusOutlined />}
            onClick={() => onAddClassification?.()}
          />
        </Tooltip>
      </div>

      <div
        ref={treeContainerRef}
        className="classification-tree-scroll-container"
        onContextMenu={(e) => {
          // Handle context menu on empty areas (not on tree nodes)
          const target = e.target as HTMLElement;
          // Check if click is on tree node or empty area
          if (!target.closest(".ant-tree-node-content-wrapper")) {
            e.stopPropagation();
            handleContextMenu(e, ""); // Empty string for empty area context menu
          }
        }}
      >
        <div className="classification-tree-inner">
          <Tree
            className="classification-tree"
            showIcon
            draggable
            selectedKeys={selectedNode ? [selectedNode.id] : []}
            expandedKeys={expandedKeys}
            onSelect={handleSelect}
            onRightClick={handleRightClick}
            onDragStart={(info) => {
              const nodeKey = info.node.key as string;
              draggedNodeKeyRef.current = nodeKey;

              // Set dataTransfer data for our custom drag/drop hook
              if (info.event.dataTransfer) {
                info.event.dataTransfer.setData('application/tree-node-id', nodeKey);
                info.event.dataTransfer.effectAllowed = 'move';
              }
            }}
            onDragEnd={() => {
              draggedNodeKeyRef.current = undefined;
            }}
            treeData={treeData}
          />
        </div>
        <TreeContextMenu
          items={contextMenuItems}
          visible={contextMenuNode !== undefined}
          position={contextMenuPosition}
          onClose={handleContextMenuClose}
        />
      </div>
    </div>
  );
};

/**
 * Classification tree component displaying hierarchical character categories.
 *
 * This component uses a context provider to manage all state and logic internally.
 * It provides a clean API for parent components while keeping internal complexity isolated.
 */
export const ClassificationTree: React.FC<ClassificationTreeProps> = (
  props,
) => {
  return (
    <ClassificationTreeProvider {...props}>
      <ClassificationTreeInner />
    </ClassificationTreeProvider>
  );
};

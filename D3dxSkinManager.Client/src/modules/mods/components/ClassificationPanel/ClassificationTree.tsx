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
import { logger } from "../../../core/utils/logger";
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
    .filter((item): item is NonNullable<typeof item> => item !== null)
    .map(item => {
      // Handle divider type
      if ('type' in item && item.type === 'divider') {
        return { type: 'divider' as const };
      }
      // Handle regular menu items
      return {
        key: String((item as any).key || ''),
        label: String((item as any).label || ''),
        icon: (item as any).icon,
        danger: (item as any).danger,
        disabled: (item as any).disabled,
        onClick: (item as any).onClick,
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
  selectedNode: ClassificationNode | null;

  /**
   * Callback when a node is selected
   */
  onSelect: (node: ClassificationNode | null) => void;

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
 * Internal tree component that uses the context
 */
const ClassificationTreeInner: React.FC = () => {
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
  const draggedNodeKeyRef = React.useRef<string | null>(null);

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
      <div
        style={{
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          height: "100%",
          padding: "24px",
        }}
      >
        <Spin description="Loading classification tree..." />
      </div>
    );
  }

  if (tree.length === 0) {
    return (
      <>
        <div
          style={{
            padding: "16px",
            userSelect: "none",
            height: "100%",
            minHeight: "300px",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            paddingBottom: "80px",
          }}
          onContextMenu={(e) => {
            e.preventDefault();
            setContextMenuPosition({ x: e.clientX, y: e.clientY });
            setContextMenuNode(null); // null for empty tree
          }}
        >
          <Empty
            description="No classification data available. Right-click to add one."
            image={Empty.PRESENTED_IMAGE_SIMPLE}
          />
        </div>
        <ContextMenu
          items={convertMenuItems(contextMenuItems)}
          visible={contextMenuNode !== null}
          position={contextMenuPosition}
          onClose={() => setContextMenuNode(null)}
        />
      </>
    );
  }

  return (
    <div
      style={{
        userSelect: "none",
        flex: 1,
        minHeight: 0,
        display: "flex",
        flexDirection: "column",
      }}
    >
      <div
        style={{
          display: "flex",
          gap: "8px",
          padding: "8px 8px 8px 8px",
          flexShrink: 0,
        }}
      >
        <Search
          placeholder="Search classifications..."
          value={searchQuery}
          onChange={(e) => onSearchChange(e.target.value)}
          style={{ flex: 1 }}
          allowClear
        />
        <Tooltip title="Add Classification" placement="top">
          <Button
            type="default"
            icon={<PlusOutlined />}
            onClick={() => onAddClassification?.()}
          />
        </Tooltip>
      </div>

      <div
        ref={treeContainerRef}
        style={{ flex: 1, minHeight: 0, overflow: "auto", paddingRight: "4px" }}
      >
        <div
          style={{ paddingLeft: "8px", paddingBottom: "8px" }}
          onContextMenu={(e) => {
            // Handle context menu on empty areas (not on tree nodes)
            const target = e.target as HTMLElement;
            // Check if click is on tree node or empty area
            if (!target.closest(".ant-tree-node-content-wrapper")) {
              e.preventDefault();
              e.stopPropagation();
              setContextMenuPosition({ x: e.clientX, y: e.clientY });
              setContextMenuNode(""); // Empty string for empty area context menu
            }
          }}
        >
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
              draggedNodeKeyRef.current = null;
            }}
            treeData={treeData}
            style={{ background: "transparent" }}
          />
        </div>
        <ContextMenu
          items={convertMenuItems(contextMenuItems)}
          visible={contextMenuNode !== null}
          position={contextMenuPosition}
          onClose={() => setContextMenuNode(null)}
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

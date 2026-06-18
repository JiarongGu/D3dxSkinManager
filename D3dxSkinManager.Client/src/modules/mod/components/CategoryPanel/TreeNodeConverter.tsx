import React from 'react';
import { FolderOutlined, FolderOpenOutlined, FileOutlined, LockFilled, UnlockOutlined } from '@ant-design/icons';
import type { DataNode } from 'antd/es/tree';
import { CategoryInfo } from '../../../../shared/types/category.types';
import { ModInfo } from '../../../../shared/types/mod.types';
import { toAppUrl } from '../../../../shared/utils/imageUrlHelper';
import './TreeNodeConverter.css';

/** Group active/loaded mods by their category id (one category usually has ≤1 active, but allow many). */
export function groupModsByCategory(mods: ModInfo[]): Map<string, ModInfo[]> {
  const map = new Map<string, ModInfo[]>();
  for (const mod of mods) {
    const list = map.get(mod.category);
    if (list) list.push(mod);
    else map.set(mod.category, [mod]);
  }
  return map;
}

/**
 * Active mods that should make THIS node show the indicator. A leaf or an expanded parent shows only
 * its own active mods (its children render their own dots). A COLLAPSED parent aggregates all
 * descendants' active mods, so "something loaded inside" is visible without expanding.
 */
export function activeModsForNode(
  node: CategoryInfo,
  activeByCategory: Map<string, ModInfo[]>,
  expandedKeys: React.Key[],
): ModInfo[] {
  const direct = activeByCategory.get(node.id) ?? [];
  if (node.children.length === 0 || expandedKeys.includes(node.id)) return direct;
  const acc = direct.slice();
  const walk = (n: CategoryInfo) =>
    n.children.forEach((c) => {
      const a = activeByCategory.get(c.id);
      if (a) acc.push(...a);
      walk(c);
    });
  walk(node);
  return acc;
}


/**
 * Converts CategoryInfo to Ant Design DataNode
 * Uses expandedKeys to show folder open/closed state and lockedExpandedKeys for lock indicator
 */
export function convertToDataNode(
  node: CategoryInfo,
  expandedKeys: React.Key[],
  lockedExpandedKeys: Set<string>,
  onUnlockExpanded?: (nodeId: string, e: React.MouseEvent) => void,
  onLockExpanded?: (nodeId: string, e: React.MouseEvent) => void,
  activeByCategory?: Map<string, ModInfo[]>
): DataNode {
  const isLeaf = node.children.length === 0;
  const hasThumbnail = !!node.thumbnail;
  const isExpanded = expandedKeys.includes(node.id);
  const isLocked = lockedExpandedKeys.has(node.id);
  const isParent = !isLeaf;
  const activeMods = activeModsForNode(node, activeByCategory ?? new Map(), expandedKeys);
  const isActive = activeMods.length > 0;

  // Determine which icon to show for folders
  const getFolderIcon = () => {
    // Leaf nodes (no children) show a file icon to differentiate from parent categories
    if (isLeaf) return <FileOutlined />;
    return isExpanded ? <FolderOpenOutlined /> : <FolderOutlined />;
  };

  return {
    key: node.id,
    title: (
      <span
        data-node-id={node.id}
        className="category-tree-node-title"
      >
        {hasThumbnail && node.thumbnail ? (
          <img
            src={toAppUrl(node.thumbnail) || undefined}
            alt={node.name}
            className="category-tree-node-thumbnail"
          />
        ) : (
          <span className="category-tree-node-icon-container">
            {getFolderIcon()}
          </span>
        )}
        <span className="category-tree-node-name">{node.name}</span>
        {node.modCount !== undefined && node.modCount > 0 && (
          <span className="category-tree-node-mod-count">
            ({node.modCount})
          </span>
        )}
        {isActive && (
          <span
            className="category-tree-node-active-dot"
            title={activeMods.map((m) => m.name).join(', ')}
          />
        )}
        {isParent && (
          <span className="category-tree-node-lock-container">
            {/* Locked icon (visible when locked) - click to unlock */}
            {isLocked && (
              <span
                className="category-tree-node-lock-indicator locked"
                onClick={(e) => {
                  e.stopPropagation();
                  if (onUnlockExpanded) {
                    onUnlockExpanded(node.id, e);
                  }
                }}
              >
                <LockFilled />
              </span>
            )}
            {/* Unlocked icon (visible on hover) - click to lock */}
            {!isLocked && (
              <span
                className="category-tree-node-lock-indicator unlocked"
                onClick={(e) => {
                  e.stopPropagation();
                  if (onLockExpanded) {
                    onLockExpanded(node.id, e);
                  }
                }}
              >
                <UnlockOutlined />
              </span>
            )}
          </span>
        )}
      </span>
    ),
    // Don't use the icon property at all - embed in title instead
    icon: <span />,
    isLeaf,
    children: node.children.map(child => convertToDataNode(child, expandedKeys, lockedExpandedKeys, onUnlockExpanded, onLockExpanded, activeByCategory)),
  };
}

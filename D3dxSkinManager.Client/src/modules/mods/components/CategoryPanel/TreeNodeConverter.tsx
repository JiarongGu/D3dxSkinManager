import React from 'react';
import { FolderOutlined, FolderOpenOutlined, UserOutlined } from '@ant-design/icons';
import type { DataNode } from 'antd/es/tree';
import { CategoryInfo } from '../../../../shared/types/category.types';
import { toAppUrl } from '../../../../shared/utils/imageUrlHelper';
import './TreeNodeConverter.css';

/**
 * Converts CategoryInfo to Ant Design DataNode
 * Uses expandedKeys to show folder open/closed state
 */
export function convertToDataNode(
  node: CategoryInfo,
  expandedKeys: React.Key[]
): DataNode {
  const isLeaf = node.children.length === 0;
  const hasThumbnail = !!node.thumbnail;
  const isExpanded = expandedKeys.includes(node.id);

  // Determine which icon to show for folders
  const getFolderIcon = () => {
    if (isLeaf) return <UserOutlined />;
    return isExpanded ? <FolderOpenOutlined /> : <FolderOutlined />;
  };

  return {
    key: node.id,
    title: (
      <span
        data-node-id={node.id}
        className="tree-node-title"
      >
        {hasThumbnail && node.thumbnail ? (
          <img
            src={toAppUrl(node.thumbnail) || undefined}
            alt={node.name}
            className="tree-node-thumbnail"
          />
        ) : (
          <span className="tree-node-icon-container">
            {getFolderIcon()}
          </span>
        )}
        <span className="tree-node-name">{node.name}</span>
        {node.modCount !== undefined && node.modCount > 0 && (
          <span className="tree-node-mod-count">
            ({node.modCount})
          </span>
        )}
      </span>
    ),
    // Don't use the icon property at all - embed in title instead
    icon: <span />,
    isLeaf,
    children: node.children.map(child => convertToDataNode(child, expandedKeys)),
  };
}

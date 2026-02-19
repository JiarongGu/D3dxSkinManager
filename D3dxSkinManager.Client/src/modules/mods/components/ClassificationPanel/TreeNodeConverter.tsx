import React from 'react';
import { FolderOutlined, FolderOpenOutlined, UserOutlined } from '@ant-design/icons';
import type { DataNode } from 'antd/es/tree';
import { ClassificationNode } from '../../../../shared/types/classification.types';
import { toAppUrl } from '../../../../shared/utils/imageUrlHelper';

/**
 * Converts ClassificationNode to Ant Design DataNode
 * Uses expandedKeys to show folder open/closed state
 */
export function convertToDataNode(
  node: ClassificationNode,
  expandedKeys: React.Key[],
  onModDragOver?: (e: React.DragEvent, nodeId: string) => void,
  onModDrop?: (e: React.DragEvent, nodeId: string) => Promise<void>
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
        style={{
          display: 'inline-flex',
          alignItems: 'center',
          gap: '6px',
          height: '40px',
          lineHeight: 'normal',
          width: '100%',
        }}
      >
        {hasThumbnail && node.thumbnail ? (
          <img
            src={toAppUrl(node.thumbnail) || undefined}
            alt={node.name}
            style={{
              width: '36px',
              height: '36px',
              objectFit: 'cover',
              borderRadius: '2px',
              flexShrink: 0
            }}
          />
        ) : (
          <span style={{
            fontSize: '16px',
            width: '36px',
            height: '36px',
            display: 'inline-flex',
            alignItems: 'center',
            justifyContent: 'center',
            flexShrink: 0
          }}>
            {getFolderIcon()}
          </span>
        )}
        <span style={{ flex: 1 }}>{node.name}</span>
        {node.modCount !== undefined && node.modCount > 0 && (
          <span
            style={{
              fontSize: '12px',
              color: '#8c8c8c',
              marginLeft: '8px',
              flexShrink: 0
            }}
          >
            ({node.modCount})
          </span>
        )}
      </span>
    ),
    // Don't use the icon property at all - embed in title instead
    icon: <span />,
    isLeaf,
    children: node.children.map(child => convertToDataNode(child, expandedKeys, onModDragOver, onModDrop)),
  };
}

import React from 'react';
import { FolderAddOutlined, PlusOutlined, EditOutlined, DeleteOutlined } from '@ant-design/icons';
import type { MenuProps } from 'antd';

interface CategoryContextMenuProps {
  nodeId: string | undefined;
  onAddCategory?: (parentId?: string) => void;
  onEditNode: (nodeId: string) => void;
  onDeleteNode: (nodeId: string) => void;
}

/**
 * Generates context menu items for Category tree nodes
 */
export function getCategoryContextMenu({
  nodeId,
  onAddCategory,
  onEditNode,
  onDeleteNode,
}: CategoryContextMenuProps): MenuProps['items'] {
  // If nodeId is empty string or undefined, show "Add Category" for root
  // If nodeId has a value, show both "Add Category" (root) and "Add Sub-Category" (child)
  const items: MenuProps['items'] = [
    {
      key: 'add-root',
      label: 'Add Category',
      icon: <FolderAddOutlined />,
      onClick: () => {
        if (onAddCategory) {
          onAddCategory(); // No parent = root Category
        }
      },
    },
  ];

  // Add "Add Sub-Category", "Edit", and "Delete" options only when right-clicking on a node
  if (nodeId && nodeId !== '') {
    items.push({
      key: 'add-child',
      label: 'Add Sub-Category',
      icon: <PlusOutlined />,
      onClick: () => {
        if (onAddCategory) {
          onAddCategory(nodeId);
        }
      },
    });

    items.push({ key: 'divider-1', type: 'divider' });

    items.push({
      key: 'edit',
      label: 'Edit',
      icon: <EditOutlined />,
      onClick: () => onEditNode(nodeId),
    });

    items.push({
      key: 'delete',
      label: 'Delete',
      icon: <DeleteOutlined />,
      danger: true,
      onClick: () => onDeleteNode(nodeId),
    });
  }

  return items;
}

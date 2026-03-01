import React from 'react';
import { FolderAddOutlined, PlusOutlined, EditOutlined, DeleteOutlined } from '@ant-design/icons';
import type { MenuProps } from 'antd';
import type { TFunction } from 'i18next';

interface CategoryContextMenuProps {
  nodeId: string | undefined;
  onAddCategory?: (parentId?: string) => void;
  onEditNode: (nodeId: string) => void;
  onDeleteNode: (nodeId: string) => void;
  t: TFunction;
}

/**
 * Generates context menu items for Category tree nodes
 * Order: Add Sub-Category, Add Root-Category, divider, Edit, Delete
 */
export function getCategoryContextMenu({
  nodeId,
  onAddCategory,
  onEditNode,
  onDeleteNode,
  t,
}: CategoryContextMenuProps): MenuProps['items'] {
  const items: MenuProps['items'] = [];

  // Add "Add Sub-Category" option only when right-clicking on a node
  if (nodeId && nodeId !== '') {
    items.push({
      key: 'add-child',
      label: t('category.tree.addSubCategory'),
      icon: <PlusOutlined />,
      onClick: () => {
        if (onAddCategory) {
          onAddCategory(nodeId);
        }
      },
    });
  }

  // Add "Add Root-Category" option (always visible)
  items.push({
    key: 'add-root',
    label: t('category.tree.addRootCategory'),
    icon: <FolderAddOutlined />,
    onClick: () => {
      if (onAddCategory) {
        onAddCategory(); // No parent = root Category
      }
    },
  });

  // Add divider, "Edit", and "Delete" options only when right-clicking on a node
  if (nodeId && nodeId !== '') {
    items.push({ key: 'divider-1', type: 'divider' });

    items.push({
      key: 'edit',
      label: t('category.tree.edit'),
      icon: <EditOutlined />,
      onClick: () => onEditNode(nodeId),
    });

    items.push({
      key: 'delete',
      label: t('category.tree.delete'),
      icon: <DeleteOutlined />,
      danger: true,
      onClick: () => onDeleteNode(nodeId),
    });
  }

  return items;
}

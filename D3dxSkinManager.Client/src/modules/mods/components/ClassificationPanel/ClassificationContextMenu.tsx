import React from 'react';
import { FolderAddOutlined, PlusOutlined, EditOutlined, DeleteOutlined } from '@ant-design/icons';
import type { MenuProps } from 'antd';

interface ClassificationContextMenuProps {
  nodeId: string | null;
  onAddClassification?: (parentId?: string) => void;
  onEditNode: (nodeId: string) => void;
  onDeleteNode: (nodeId: string) => void;
}

/**
 * Generates context menu items for classification tree nodes
 */
export function getClassificationContextMenu({
  nodeId,
  onAddClassification,
  onEditNode,
  onDeleteNode,
}: ClassificationContextMenuProps): MenuProps['items'] {
  // If nodeId is empty string or null, show "Add Classification" for root
  // If nodeId has a value, show both "Add Classification" (root) and "Add Sub-Classification" (child)
  const items: MenuProps['items'] = [
    {
      key: 'add-root',
      label: 'Add Classification',
      icon: <FolderAddOutlined />,
      onClick: () => {
        if (onAddClassification) {
          onAddClassification(); // No parent = root classification
        }
      },
    },
  ];

  // Add "Add Sub-Classification", "Edit", and "Delete" options only when right-clicking on a node
  if (nodeId && nodeId !== '') {
    items.push({
      key: 'add-child',
      label: 'Add Sub-Classification',
      icon: <PlusOutlined />,
      onClick: () => {
        if (onAddClassification) {
          onAddClassification(nodeId);
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

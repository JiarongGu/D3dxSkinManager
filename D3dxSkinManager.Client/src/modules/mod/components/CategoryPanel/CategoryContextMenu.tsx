import React from 'react';
import { FolderAddOutlined, PlusOutlined, EditOutlined, DeleteOutlined, ExportOutlined, RadarChartOutlined, ThunderboltOutlined, PoweroffOutlined } from '@ant-design/icons';
import type { MenuProps } from 'antd';
import type { TFunction } from 'i18next';
import type { ModFixTool } from '../../../../shared/types/modFix.types';

interface CategoryContextMenuProps {
  nodeId: string | undefined;
  onAddCategory?: (parentId?: string) => void;
  onEditNode: (nodeId: string) => void;
  onDeleteNode: (nodeId: string) => void;
  onExportCategory?: (nodeId: string) => void;
  onAnalyzeCategory?: (nodeId: string) => void;
  /** Registered fix tools — used to build the "Fix all in category" submenu. */
  fixTools?: ModFixTool[];
  onRunCategoryFix?: (nodeId: string, entryPath: string, recompress: boolean) => void;
  onUnloadCategory?: (nodeId: string) => void;
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
  onExportCategory,
  onAnalyzeCategory,
  fixTools,
  onRunCategoryFix,
  onUnloadCategory,
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

  // Node-specific actions only when right-clicking on a node
  if (nodeId && nodeId !== '') {
    items.push({ key: 'divider-1', type: 'divider' });

    items.push({
      key: 'edit',
      label: t("common.edit"),
      icon: <EditOutlined />,
      onClick: () => onEditNode(nodeId),
    });

    items.push({
      key: 'export',
      label: t("category.tree.exportCategory"),
      icon: <ExportOutlined />,
      onClick: () => {
        if (onExportCategory) {
          onExportCategory(nodeId);
        }
      },
    });

    items.push({
      key: 'analyze',
      label: t("category.tree.analyzeCategory"),
      icon: <RadarChartOutlined />,
      onClick: () => {
        if (onAnalyzeCategory) {
          onAnalyzeCategory(nodeId);
        }
      },
    });

    items.push({ key: 'divider-fix', type: 'divider' });

    // Unload every loaded mod in this category in one go.
    items.push({
      key: 'unload-category',
      label: t("category.tree.unloadCategory"),
      icon: <PoweroffOutlined />,
      onClick: () => onUnloadCategory?.(nodeId),
    });

    // "Fix all in category" → submenu of fix tools (flattened to "Toolset — entry" for multi-entry).
    const fixChildren: NonNullable<MenuProps['items']> = [];
    for (const tf of fixTools ?? []) {
      if (tf.entries.length === 0) {
        fixChildren.push({ key: `catfix-${tf.id}`, label: `${tf.name} — ${t('tools.modFix.selectEntry')}`, disabled: true });
      } else if (tf.entries.length === 1) {
        const e = tf.entries[0];
        fixChildren.push({ key: `catfix-${tf.id}`, label: tf.name, onClick: () => onRunCategoryFix?.(nodeId, e.path, tf.recompressDefault) });
      } else {
        for (const e of tf.entries) {
          fixChildren.push({ key: `catfix-${tf.id}-${e.name}`, label: `${tf.name} — ${e.name}`, onClick: () => onRunCategoryFix?.(nodeId, e.path, tf.recompressDefault) });
        }
      }
    }
    if (fixChildren.length === 0) {
      fixChildren.push({ key: 'catfix-none', label: t('contextMenu.noFixTools'), disabled: true });
    }
    items.push({
      key: 'fix-category',
      label: t("category.tree.fixCategory"),
      icon: <ThunderboltOutlined />,
      children: fixChildren,
    });

    items.push({ key: 'divider-2', type: 'divider' });

    items.push({
      key: 'delete',
      label: t("common.delete"),
      icon: <DeleteOutlined />,
      danger: true,
      onClick: () => onDeleteNode(nodeId),
    });
  }

  return items;
}

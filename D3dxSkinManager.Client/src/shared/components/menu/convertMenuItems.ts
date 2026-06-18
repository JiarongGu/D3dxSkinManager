import type React from 'react';
import type { MenuProps } from 'antd';
import type { ContextMenuItem } from './ContextMenu';

/**
 * Convert antd `MenuProps['items']` (the shape the category menu builders produce) into the custom
 * `ContextMenuItem[]` the shared `ContextMenu` renders. Recurses `children` so **submenus are preserved**
 * (a previous duplicate of this in CategoryTree + CategoryGrid dropped `children`, silently flattening
 * submenus like "Fix all in category"). Shared so there's one correct implementation.
 */
export function convertMenuItems(items: MenuProps['items']): ContextMenuItem[] {
  if (!items) return [];
  return items
    .filter((item): item is NonNullable<typeof item> => item != null)
    .map((item) => {
      if ('type' in item && item.type === 'divider') {
        return { type: 'divider' as const };
      }
      const menuItem = item as {
        key?: string | number;
        label?: React.ReactNode;
        icon?: React.ReactNode;
        danger?: boolean;
        disabled?: boolean;
        onClick?: () => void;
        children?: MenuProps['items'];
      };
      return {
        key: String(menuItem.key || ''),
        label: menuItem.label,
        icon: menuItem.icon,
        danger: menuItem.danger,
        disabled: menuItem.disabled,
        onClick: menuItem.onClick,
        children: menuItem.children ? convertMenuItems(menuItem.children) : undefined,
      };
    });
}

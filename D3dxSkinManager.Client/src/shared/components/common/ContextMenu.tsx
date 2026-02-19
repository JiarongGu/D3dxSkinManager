import React from 'react';
import { Dropdown } from 'antd';
import type { MenuProps } from 'antd';

export interface ContextMenuItem {
  key: string;
  label?: string;
  icon?: React.ReactNode;
  danger?: boolean;
  disabled?: boolean;
  visible?: boolean;
  onClick?: () => void;
  children?: ContextMenuItem[];
  type?: 'divider';
}

export interface ContextMenuProps {
  items: ContextMenuItem[];
  children: React.ReactElement;
  trigger?: ('click' | 'hover' | 'contextMenu')[];
}

/**
 * Reusable context menu component with conditional visibility and disabled states
 */
export const ContextMenu: React.FC<ContextMenuProps> = ({
  items,
  children,
  trigger = ['contextMenu'],
}) => {
  // Filter out items that are explicitly hidden
  const filterItems = (menuItems: ContextMenuItem[]): ContextMenuItem[] => {
    return menuItems
      .filter(item => item.visible !== false)
      .map(item => {
        if (item.children) {
          return {
            ...item,
            children: filterItems(item.children),
          };
        }
        return item;
      });
  };

  // Convert our ContextMenuItem format to Ant Design MenuProps format
  const convertToMenuItems = (menuItems: ContextMenuItem[]): MenuProps['items'] => {
    return menuItems.map(item => {
      if (item.type === 'divider') {
        return { type: 'divider' as const, key: item.key };
      }

      if (item.children) {
        return {
          key: item.key,
          label: item.label,
          icon: item.icon,
          disabled: item.disabled,
          children: convertToMenuItems(item.children),
        };
      }

      return {
        key: item.key,
        label: item.label,
        icon: item.icon,
        danger: item.danger,
        disabled: item.disabled,
        onClick: item.onClick,
      };
    });
  };

  const visibleItems = filterItems(items);
  const menuItems = convertToMenuItems(visibleItems);

  // Don't render dropdown if no visible items
  if (visibleItems.length === 0) {
    return children;
  }

  return (
    <Dropdown
      menu={{ items: menuItems }}
      trigger={trigger}
    >
      {children}
    </Dropdown>
  );
};

import React, { useEffect, useRef, useState } from 'react';
import classNames from 'classnames';
import './ContextMenu.css';

export interface ContextMenuItem {
  key?: string;
  label?: React.ReactNode;
  icon?: React.ReactNode;
  danger?: boolean;
  disabled?: boolean;
  visible?: boolean;
  onClick?: () => void;
  type?: 'divider';
}

export interface ContextMenuProps {
  /**
   * Menu items to display
   */
  items: ContextMenuItem[];

  /**
   * Whether the menu is visible
   */
  visible: boolean;

  /**
   * Position of the menu (from mouse event)
   */
  position: { x: number; y: number };

  /**
   * Callback when menu should close
   */
  onClose: () => void;
}

/**
 * Custom context menu component for right-click menus and dropdowns.
 * Provides smooth positioning, animations, and theme-aware styling.
 * Can be used for right-click context menus or dropdown menus from buttons.
 */
export const ContextMenu: React.FC<ContextMenuProps> = ({
  items,
  visible,
  position,
  onClose,
}) => {
  const menuRef = useRef<HTMLDivElement>(null);
  const [menuPosition, setMenuPosition] = useState({ x: position.x, y: position.y });
  const [isReady, setIsReady] = useState(false);
  const [expandFromBottom, setExpandFromBottom] = useState(false);

  // Calculate position when menu becomes visible
  useEffect(() => {
    if (!visible) {
      setIsReady(false);
      return;
    }

    if (!menuRef.current) return;

    // Wait for next frame to ensure menu is rendered and measurable
    const timer = setTimeout(() => {
      if (!menuRef.current) return;

      const menuRect = menuRef.current.getBoundingClientRect();
      const viewportHeight = window.innerHeight;
      const viewportWidth = window.innerWidth;

      let x = position.x;
      let y = position.y;
      let shouldExpandFromBottom = false;
      const offset = 4;

      // Position menu above click point if it would overflow bottom
      if (y + menuRect.height > viewportHeight - 10) {
        y = y - menuRect.height - offset;
        shouldExpandFromBottom = true;
      } else {
        y = y + offset;
      }

      // Position menu to the left if it would overflow right edge
      if (x + menuRect.width > viewportWidth - 10) {
        x = x - menuRect.width - offset;
      } else {
        x = x + offset;
      }

      // Ensure menu stays within viewport bounds
      x = Math.max(10, x);
      y = Math.max(10, y);

      setExpandFromBottom(shouldExpandFromBottom);
      setMenuPosition({ x, y });
      setIsReady(true);
    }, 0);

    return () => clearTimeout(timer);
  }, [visible, position]);

  // Close menu when clicking outside or scrolling
  useEffect(() => {
    if (!visible) return;

    const handleClickOutside = (e: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        onClose();
      }
    };

    const handleEscape = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        onClose();
      }
    };

    const handleScroll = (e: Event) => {
      // Close menu on any scroll event (window or element)
      // Don't close if scrolling within the menu itself
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        onClose();
      }
    };

    // Use capture phase to catch scroll events on any element
    document.addEventListener('mousedown', handleClickOutside, true);
    document.addEventListener('keydown', handleEscape);
    document.addEventListener('scroll', handleScroll, true);

    return () => {
      document.removeEventListener('mousedown', handleClickOutside, true);
      document.removeEventListener('keydown', handleEscape);
      document.removeEventListener('scroll', handleScroll, true);
    };
  }, [visible, onClose]);

  if (!visible) return null;

  // Filter visible items
  const visibleItems = items.filter((item) => item.visible !== false);

  if (visibleItems.length === 0) return null;

  const handleItemClick = (item: ContextMenuItem) => {
    if (item.disabled) return;
    if (item.onClick) {
      item.onClick();
    }
    onClose();
  };

  return (
    <div
      ref={menuRef}
      className={classNames('context-menu', {
        'expand-bottom-up': isReady && expandFromBottom,
        'expand-top-down': isReady && !expandFromBottom
      })}
      style={{
        position: 'fixed',
        left: `${menuPosition.x}px`,
        top: `${menuPosition.y}px`,
        zIndex: 9999,
        opacity: isReady ? 1 : 0,
      }}
    >
      {visibleItems.map((item, index) => {
        if (item.type === 'divider') {
          return <div key={item.key || `divider-${index}`} className="context-menu-divider" />;
        }

        return (
          <div
            key={item.key || `item-${index}`}
            className={classNames('context-menu-item', {
              disabled: item.disabled,
              danger: item.danger
            })}
            onClick={() => handleItemClick(item)}
          >
            {item.icon && <span className="context-menu-item-icon">{item.icon}</span>}
            <span className="context-menu-item-label">{item.label}</span>
          </div>
        );
      })}
    </div>
  );
};

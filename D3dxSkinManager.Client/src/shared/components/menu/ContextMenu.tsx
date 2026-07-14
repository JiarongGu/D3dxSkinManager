import React, { useEffect, useLayoutEffect, useRef, useState } from 'react';
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
  /** Nested items — renders this row as a submenu parent with a hover flyout. */
  children?: ContextMenuItem[];
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
 * A submenu flyout that flips to the LEFT of its parent when opening at `left:100%` would overflow the
 * right viewport edge (measured once on open). Without this the flyout was clipped near the screen edge.
 */
const SubmenuFlyout: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const ref = useRef<HTMLDivElement>(null);
  const [flipLeft, setFlipLeft] = useState(false);
  useLayoutEffect(() => {
    const el = ref.current;
    if (el) setFlipLeft(el.getBoundingClientRect().right > window.innerWidth);
  }, []);
  return (
    <div
      ref={ref}
      className="context-menu context-menu-flyout"
      style={{ position: 'absolute', top: 0, zIndex: 10000, ...(flipLeft ? { right: '100%' } : { left: '100%' }) }}
    >
      {children}
    </div>
  );
};

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
  const [openSubmenu, setOpenSubmenu] = useState<string | null>(null);

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
    // Parent rows (with children) only toggle their flyout; they don't act/close.
    if (item.children && item.children.length > 0) return;
    if (item.onClick) {
      item.onClick();
    }
    onClose();
  };

  const renderRow = (item: ContextMenuItem, index: number) => {
    if (item.type === 'divider') {
      return <div key={item.key || `divider-${index}`} className="context-menu-divider" />;
    }

    const hasChildren = !!item.children && item.children.length > 0;
    const key = item.key || `item-${index}`;

    if (hasChildren) {
      const childItems = item.children!.filter((c) => c.visible !== false);
      return (
        <div
          key={key}
          className="context-menu-submenu"
          style={{ position: 'relative' }}
          onMouseEnter={() => setOpenSubmenu(key)}
          onMouseLeave={() => setOpenSubmenu((k) => (k === key ? null : k))}
        >
          <div className={classNames('context-menu-item', { disabled: item.disabled, danger: item.danger })}>
            {item.icon && <span className="context-menu-item-icon">{item.icon}</span>}
            <span className="context-menu-item-label">{item.label}</span>
            <span className="context-menu-submenu-caret">›</span>
          </div>
          {openSubmenu === key && childItems.length > 0 && (
            <SubmenuFlyout>
              {childItems.map((child, ci) => renderRow(child, ci))}
            </SubmenuFlyout>
          )}
        </div>
      );
    }

    return (
      <div
        key={key}
        className={classNames('context-menu-item', { disabled: item.disabled, danger: item.danger })}
        onClick={() => handleItemClick(item)}
      >
        {item.icon && <span className="context-menu-item-icon">{item.icon}</span>}
        <span className="context-menu-item-label">{item.label}</span>
      </div>
    );
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
      {visibleItems.map((item, index) => renderRow(item, index))}
    </div>
  );
};

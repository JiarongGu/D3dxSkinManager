import { useState, useCallback } from 'react';

export interface PopupMenuState {
  /**
   * Whether the menu is currently visible
   */
  visible: boolean;

  /**
   * Current position of the menu
   */
  position: { x: number; y: number };
}

export interface PopupMenuActions {
  /**
   * Show the menu at the given mouse event position
   */
  show: (event: React.MouseEvent) => void;

  /**
   * Hide the menu
   */
  hide: () => void;

  /**
   * Get props to spread onto the context menu trigger element
   * This automatically handles preventDefault and position tracking
   */
  getTriggerProps: () => {
    onContextMenu: (event: React.MouseEvent) => void;
  };
}

export interface UsePopupMenuReturn extends PopupMenuState, PopupMenuActions {}

/**
 * Hook for managing popup menu state and position tracking.
 * Use this for right-click context menus where you need automatic
 * position tracking from mouse events.
 *
 * @example
 * ```tsx
 * const menuState = usePopupMenu();
 *
 * // Simple usage - spread getTriggerProps()
 * <div {...menuState.getTriggerProps()}>
 *   Right-click me!
 * </div>
 * <ContextMenu
 *   items={items}
 *   visible={menuState.visible}
 *   position={menuState.position}
 *   onClose={menuState.hide}
 * />
 *
 * // Advanced usage - manual control
 * <div onContextMenu={(e) => {
 *   e.preventDefault();
 *   // Custom logic here
 *   menuState.show(e);
 * }}>
 *   Right-click me!
 * </div>
 * ```
 */
export const useContextMenu = (): UsePopupMenuReturn => {
  const [visible, setVisible] = useState(false);
  const [position, setPosition] = useState({ x: 0, y: 0 });

  const show = useCallback((event: React.MouseEvent) => {
    setPosition({ x: event.clientX, y: event.clientY });
    setVisible(true);
  }, []);

  const hide = useCallback(() => {
    setVisible(false);
  }, []);

  const getTriggerProps = useCallback(() => ({
    onContextMenu: (event: React.MouseEvent) => {
      event.preventDefault();
      show(event);
    },
  }), [show]);

  return {
    visible,
    position,
    show,
    hide,
    getTriggerProps,
  };
};

import { useRef, useEffect, useCallback } from 'react';

/**
 * Hook for persisting and restoring scroll position
 * Useful when content reloads and you want to maintain scroll position
 *
 * @param key - Unique key to identify this scroll position (e.g., 'mod-list', 'category-tree')
 * @returns Object with ref to attach to scrollable element and functions to save/restore
 *
 * @example
 * ```tsx
 * const { scrollRef, saveScrollPosition, restoreScrollPosition } = useScrollPosition('mod-list');
 *
 * useEffect(() => {
 *   // Save scroll position before reload
 *   saveScrollPosition();
 *
 *   // Reload data...
 *
 *   // Restore scroll position after data loads
 *   restoreScrollPosition();
 * }, [someData]);
 *
 * <div ref={scrollRef} className="scrollable-content">
 *   {content}
 * </div>
 * ```
 */
export function useScrollPosition(key: string) {
  const scrollRef = useRef<HTMLDivElement>(null);
  const savedPosition = useRef<number>(0);

  /**
   * Save current scroll position
   */
  const saveScrollPosition = useCallback(() => {
    if (scrollRef.current) {
      savedPosition.current = scrollRef.current.scrollTop;
    }
  }, []);

  /**
   * Restore previously saved scroll position
   * Uses requestAnimationFrame to ensure DOM has updated
   */
  const restoreScrollPosition = useCallback(() => {
    if (scrollRef.current && savedPosition.current > 0) {
      // Use requestAnimationFrame to ensure DOM is updated
      requestAnimationFrame(() => {
        if (scrollRef.current) {
          scrollRef.current.scrollTop = savedPosition.current;
        }
      });
    }
  }, []);

  /**
   * Reset saved position (useful when changing categories or filters)
   */
  const resetScrollPosition = useCallback(() => {
    savedPosition.current = 0;
    if (scrollRef.current) {
      scrollRef.current.scrollTop = 0;
    }
  }, []);

  return {
    scrollRef,
    saveScrollPosition,
    restoreScrollPosition,
    resetScrollPosition,
  };
}

import { useEffect, useRef, useCallback } from 'react';
import { bridgeService } from '../services/bridgeService';
import { eventBus, DropZoneEventType, Module } from '../services/eventBus';
import { v4 as uuidv4 } from 'uuid';
import { debounce } from 'lodash-es';
import './useDropZone.css';
import logger from '../utils/logger';

/**
 * Drop zone file drop data from backend
 */
export interface DropZoneFileDropData {
  zoneId: string;
  files: string[];
  position: { x: number; y: number };
}

/**
 * Check if element is occluded by another element with higher z-index
 */
const isElementOccluded = (elem: HTMLElement): boolean => {
  const rect = elem.getBoundingClientRect();

  // Check multiple points across the element to be thorough
  const testPoints = [
    { x: rect.left + rect.width / 2, y: rect.top + rect.height / 2 }, // center
    { x: rect.left + 10, y: rect.top + 10 }, // top-left
    { x: rect.right - 10, y: rect.top + 10 }, // top-right
    { x: rect.left + 10, y: rect.bottom - 10 }, // bottom-left
    { x: rect.right - 10, y: rect.bottom - 10 }, // bottom-right
  ];

  // If any test point is occluded, consider the element occluded
  for (const point of testPoints) {
    const topElement = document.elementFromPoint(point.x, point.y);

    // If the top element is our element OR a descendant of it, it's NOT occluded
    // This allows child elements (like mod list items) to exist without being considered occlusion
    if (topElement && (topElement === elem || elem.contains(topElement))) {
      continue; // This point is not occluded
    }

    // If we get here, the top element is NOT our element or a child
    // This means something else is on top - the drop zone IS occluded
    if (topElement) {
      return true;
    }
  }

  return false;
};

/**
 * Hook to create a WinForms drop zone overlay that syncs with a web element
 *
 * This creates a transparent WinForms panel that overlays the target element,
 * captures file drops at the OS level (with real paths), and syncs position/size
 * automatically when the element moves, resizes, or scrolls.
 *
 * Features:
 * - Captures real OS file paths (not blob URLs)
 * - Click-through when not dragging (WM_NCHITTEST)
 * - Auto-tracks element position with ResizeObserver + IntersectionObserver
 * - Adds CSS classes for visual feedback: 'use-drop-zone-hover' and 'use-drop-zone-drop' (styles in useDropZone.css)
 *
 * @param options Configuration options
 * @param options.targetRef Ref to the DOM element to track
 * @param options.onDrop Callback when files are dropped with real OS paths
 * @param options.enabled Whether the drop zone is active (default: true)
 * @param options.zoneId Optional custom zone ID (auto-generated if not provided)
 * @param options.classes Optional CSS class names for different states
 * @param options.classes.hover CSS class for hover state (default: 'use-drop-zone-hover')
 * @param options.classes.drop CSS class for drag-over state (default: 'use-drop-zone-drop')
 *
 * @example
 * const uploadRef = useRef<HTMLDivElement>(null);
 *
 * useDropZone({
 *   targetRef: uploadRef,
 *   onDrop: (files) => {
 *     logger.log('Real paths:', files);
 *     // files[0] = "C:\\Users\\...\\image.jpg"
 *   }
 * });
 *
 * // With custom classes:
 * useDropZone({
 *   targetRef: uploadRef,
 *   onDrop: handleDrop,
 *   classes: {
 *     hover: 'my-hover-class',
 *     drop: 'my-drop-class'
 *   }
 * });
 *
 * return <div ref={uploadRef}>Drop files here</div>;
 */
export function useDropZone(options: {
  targetRef: React.RefObject<HTMLElement | null>;
  onDrop: (files: string[]) => void;
  enabled?: boolean;
  zoneId?: string;
  classes?: {
    hover?: string;
    drop?: string;
  };
}) {
  const { targetRef, onDrop, enabled = true, zoneId: customZoneId, classes } = options;

  // Generate stable zone ID
  const zoneIdRef = useRef(customZoneId || `drop-zone-${uuidv4()}`);

  // Store class names as stable references (they never change)
  const classesRef = useRef({
    hover: classes?.hover ?? 'use-drop-zone-hover',
    drop: classes?.drop ?? 'use-drop-zone-drop'
  });

  // Store latest callback to avoid re-subscribing
  const onDropRef = useRef(onDrop);
  useEffect(() => {
    onDropRef.current = onDrop;
  }, [onDrop]);

  // Track if zone is registered
  const isRegisteredRef = useRef(false);

  // Track last known bounds to avoid redundant updates
  const lastBoundsRef = useRef({ x: 0, y: 0, width: 0, height: 0 });

  // Function to update zone position
  const updateZoneImmediate = useCallback(() => {
    if (!targetRef.current) return;

    const element = targetRef.current;
    const rect = element.getBoundingClientRect();

    if (rect.width === 0 || rect.height === 0) {
      // Element not visible, hide zone
      if (isRegisteredRef.current) {
        bridgeService.sendMessage({
          module: 'DROP_ZONE',
          type: 'HIDE',
          payload: { zoneId: zoneIdRef.current }
        }).catch(err => {
          logger.error('[useDropZone] Failed to hide zone:', err);
        });
      }
      return;
    }

    // Check if element is occluded by another element
    if (isElementOccluded(element)) {
      logger.verbose("[useDropZone] occluded to hide zone")
      // Element is covered, hide zone
      if (isRegisteredRef.current) {
        bridgeService.sendMessage({
          module: 'DROP_ZONE',
          type: 'HIDE',
          payload: { zoneId: zoneIdRef.current }
        }).catch(err => {
          logger.error('[useDropZone] Failed to hide zone (occluded):', err);
        });
      }
      // Reset lastBounds so we'll update when occlusion is removed
      lastBoundsRef.current = { x: -1, y: -1, width: -1, height: -1 };
      return;
    }

    const bounds = {
      zoneId: zoneIdRef.current,
      x: Math.round(rect.left),
      y: Math.round(rect.top),
      width: Math.round(rect.width),
      height: Math.round(rect.height)
    };

    // Skip if bounds haven't changed (but only if bounds are valid)
    if (
      isRegisteredRef.current &&
      lastBoundsRef.current.x >= 0 && // Check if lastBounds is valid (not reset)
      bounds.x === lastBoundsRef.current.x &&
      bounds.y === lastBoundsRef.current.y &&
      bounds.width === lastBoundsRef.current.width &&
      bounds.height === lastBoundsRef.current.height
    ) {
      return;
    }

    lastBoundsRef.current = bounds;

    if (!isRegisteredRef.current) {
      // Register new zone
      bridgeService.sendMessage({
        module: 'DROP_ZONE',
        type: 'REGISTER',
        payload: bounds
      }).then(() => {
        isRegisteredRef.current = true;
      }).catch(err => {
        logger.error('[useDropZone] Failed to register zone:', err);
      });
    } else {
      // Update existing zone (and show it if it was hidden)
      bridgeService.sendMessage({
        module: 'DROP_ZONE',
        type: 'UPDATE',
        payload: bounds
      }).then(() => {
        // Ensure zone is visible after update
        bridgeService.sendMessage({
          module: 'DROP_ZONE',
          type: 'SHOW',
          payload: { zoneId: zoneIdRef.current }
        }).catch(err => {
          logger.error('[useDropZone] Failed to show zone:', err);
        });
      }).catch(err => {
        logger.error('[useDropZone] Failed to update zone:', err);
      });
    }
  }, []);

  // Debounce updateZone to prevent chain actions (10ms)
  const updateZone = useRef(debounce(updateZoneImmediate, 10)).current;

  // Register/update drop zone when element bounds change
  useEffect(() => {
    // If disabled or element not in DOM, hide the zone if it was previously registered
    if (!enabled || !targetRef.current) {
      if (isRegisteredRef.current) {
        bridgeService.sendMessage({
          module: 'DROP_ZONE',
          type: 'HIDE',
          payload: { zoneId: zoneIdRef.current }
        }).catch(err => {
          logger.error('[useDropZone] Failed to hide zone:', err);
        });
      }
      return;
    }

    const element = targetRef.current;

    // Initial registration
    updateZone();

    // Use ResizeObserver to detect element size changes
    const resizeObserver = new ResizeObserver(() => {
      updateZone();
    });
    resizeObserver.observe(element);

    // Use IntersectionObserver to detect element visibility/position changes
    const intersectionObserver = new IntersectionObserver(() => {
      updateZone();
    }, {
      threshold: [0, 0.1, 0.5, 0.9, 1.0] // Multiple thresholds for better detection
    });
    intersectionObserver.observe(element);

    // Update on scroll (element position changes)
    const handleScroll = () => {
      updateZone();
    };
    window.addEventListener('scroll', handleScroll, true); // Use capture to catch all scrolls

    // Update on window resize (element may reflow)
    const handleResize = () => {
      updateZone();
    };
    window.addEventListener('resize', handleResize);

    // Use MutationObserver to detect when overlays (modals, dialogs, slide-in screens) are added/removed
    const mutationObserver = new MutationObserver(() => {
      // When DOM changes (like modals opening/closing), check if zone should be updated
      updateZone();
    });

    // Observe the entire document body for added/removed overlays
    mutationObserver.observe(document.body, {
      childList: true,
      subtree: true,
      attributes: true,
      attributeFilter: ['class', 'style'] // Watch for visibility/z-index changes
    });

    return () => {
      updateZone.cancel(); // Cancel any pending debounced calls
      resizeObserver.disconnect();
      intersectionObserver.disconnect();
      mutationObserver.disconnect();
      window.removeEventListener('scroll', handleScroll, true);
      window.removeEventListener('resize', handleResize);
    };
  }, [enabled, targetRef, updateZone]);

  // Subscribe to backend drag/drop events for visual styling
  useEffect(() => {
    if (!enabled || !targetRef.current) {
      return;
    }

    const element = targetRef.current;

    // Subscribe to backend drag enter/leave/drop notifications
    const unsubscribeDragEnter = eventBus.subscribe(Module.DROP_ZONE, DropZoneEventType.DRAG_ENTER, (event) => {
      if (!event?.payload || (event.payload as any).zoneId !== zoneIdRef.current) return;
      element.classList.add(classesRef.current.drop);
    });

    const unsubscribeDragLeave = eventBus.subscribe(Module.DROP_ZONE, DropZoneEventType.DRAG_LEAVE, (event) => {
      if (!event?.payload || (event.payload as any).zoneId !== zoneIdRef.current) return;
      element.classList.remove(classesRef.current.drop);
    });

    return () => {
      unsubscribeDragEnter();
      unsubscribeDragLeave();
      // Clean up class on unmount
      element.classList.remove(classesRef.current.drop);
    };
  }, [enabled, targetRef]);

  // Subscribe to mouse enter/leave events from overlay to trigger hover state
  useEffect(() => {
    if (!enabled || !targetRef.current) {
      return;
    }

    const element = targetRef.current;

    const unsubscribeMouseEnter = eventBus.subscribe(Module.DROP_ZONE, DropZoneEventType.MOUSE_ENTER, (event) => {
      if (!event?.payload || (event.payload as any).zoneId !== zoneIdRef.current) return;

      element.classList.add(classesRef.current.hover);
    });

    const unsubscribeMouseLeave = eventBus.subscribe(Module.DROP_ZONE, DropZoneEventType.MOUSE_LEAVE, (event) => {
      if (!event?.payload || (event.payload as any).zoneId !== zoneIdRef.current) return;

      element.classList.remove(classesRef.current.hover);
    });

    return () => {
      unsubscribeMouseEnter();
      unsubscribeMouseLeave();
      // Clean up hover class on unmount
      element.classList.remove(classesRef.current.hover);
    };
  }, [enabled, targetRef]);

  // Subscribe to drop events
  useEffect(() => {
    const unsubscribe = eventBus.subscribe(Module.DROP_ZONE, DropZoneEventType.FILE_DROP, (event) => {
      if (!event) {
        return;
      }

      const data = event.payload as DropZoneFileDropData;

      if (!data) {
        logger.error('[useDropZone] Event payload is undefined:', event);
        return;
      }

      if (!data.zoneId) {
        logger.error('[useDropZone] Event payload missing zoneId:', data);
        return;
      }

      // Only handle drops for our zone
      if (data.zoneId !== zoneIdRef.current) {
        return;
      }

      // Remove drag-over styling
      if (targetRef.current) {
        targetRef.current.classList.remove(classesRef.current.drop);
      }

      // Call the drop handler
      onDropRef.current(data.files);
    });

    return () => unsubscribe();
  }, [targetRef]);

  // Unregister zone on unmount
  useEffect(() => {
    return () => {
      if (isRegisteredRef.current) {
        bridgeService.sendMessage({
          module: 'DROP_ZONE',
          type: 'UNREGISTER',
          payload: { zoneId: zoneIdRef.current }
        }).catch(err => {
          logger.error('[useDropZone] Failed to unregister zone:', err);
        });
      }
    };
  }, []);
}

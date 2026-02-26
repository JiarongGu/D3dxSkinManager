import { useEffect, useRef } from 'react';
import { bridgeService } from '../services/bridgeService';
import { eventBus, DropZoneEventType, Module } from '../services/eventBus';
import { v4 as uuidv4 } from 'uuid';
import './useDropZone.css';

/**
 * Drop zone file drop data from backend
 */
export interface DropZoneFileDropData {
  zoneId: string;
  files: string[];
  position: { x: number; y: number };
}

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
 *     console.log('Real paths:', files);
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
  const zoneId = zoneIdRef.current;

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

  // Register/update drop zone when element bounds change
  useEffect(() => {
    // If disabled or element not in DOM, hide the zone if it was previously registered
    if (!enabled || !targetRef.current) {
      if (isRegisteredRef.current) {
        bridgeService.sendMessage({
          module: 'DROP_ZONE',
          type: 'HIDE',
          payload: { zoneId }
        }).catch(err => {
          console.error('[useDropZone] Failed to hide zone:', err);
        });
      }
      return;
    }

    const element = targetRef.current;

    // Track last known bounds to avoid redundant updates
    let lastBounds = { x: 0, y: 0, width: 0, height: 0 };

    // Debounce timer for updates
    let updateTimer: NodeJS.Timeout | null = null;

    // Function to update zone position
    const updateZone = () => {
      // Cancel pending update
      if (updateTimer) {
        clearTimeout(updateTimer);
      }

      // Debounce updates to 50ms
      updateTimer = setTimeout(() => {
        const rect = element.getBoundingClientRect();

        if (rect.width === 0 || rect.height === 0) {
          // Element not visible, hide zone
          if (isRegisteredRef.current) {
            bridgeService.sendMessage({
              module: 'DROP_ZONE',
              type: 'HIDE',
              payload: { zoneId }
            }).catch(err => {
              console.error('[useDropZone] Failed to hide zone:', err);
            });
          }
          return;
        }

        const bounds = {
          zoneId,
          x: Math.round(rect.left),
          y: Math.round(rect.top),
          width: Math.round(rect.width),
          height: Math.round(rect.height)
        };

        // Skip if bounds haven't changed
        if (
          isRegisteredRef.current &&
          bounds.x === lastBounds.x &&
          bounds.y === lastBounds.y &&
          bounds.width === lastBounds.width &&
          bounds.height === lastBounds.height
        ) {
          return;
        }

        lastBounds = bounds;

        if (!isRegisteredRef.current) {
          // Register new zone
          bridgeService.sendMessage({
            module: 'DROP_ZONE',
            type: 'REGISTER',
            payload: bounds
          }).then(() => {
            isRegisteredRef.current = true;
          }).catch(err => {
            console.error('[useDropZone] Failed to register zone:', err);
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
              payload: { zoneId }
            }).catch(err => {
              console.error('[useDropZone] Failed to show zone:', err);
            });
          }).catch(err => {
            console.error('[useDropZone] Failed to update zone:', err);
          });
        }
      }, 50);
    };

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

    return () => {
      if (updateTimer) {
        clearTimeout(updateTimer);
      }
      resizeObserver.disconnect();
      intersectionObserver.disconnect();
      window.removeEventListener('scroll', handleScroll, true);
      window.removeEventListener('resize', handleResize);
    };
  }, [enabled, targetRef, zoneId]);

  // Subscribe to backend drag/drop events for visual styling
  useEffect(() => {
    if (!enabled || !targetRef.current) {
      return;
    }

    const element = targetRef.current;

    // Subscribe to backend drag enter/leave/drop notifications
    const unsubscribeDragEnter = eventBus.subscribe(Module.DROP_ZONE, DropZoneEventType.DRAG_ENTER, (event) => {
      if (!event?.payload || (event.payload as any).zoneId !== zoneId) return;
      element.classList.add(classesRef.current.drop);
    });

    const unsubscribeDragLeave = eventBus.subscribe(Module.DROP_ZONE, DropZoneEventType.DRAG_LEAVE, (event) => {
      if (!event?.payload || (event.payload as any).zoneId !== zoneId) return;
      element.classList.remove(classesRef.current.drop);
    });

    return () => {
      unsubscribeDragEnter();
      unsubscribeDragLeave();
      // Clean up class on unmount
      element.classList.remove(classesRef.current.drop);
    };
  }, [enabled, targetRef, zoneId]);

  // Subscribe to click events from overlay
  useEffect(() => {
    if (!enabled || !targetRef.current) {
      return;
    }

    const element = targetRef.current;

    const unsubscribeClick = eventBus.subscribe(Module.DROP_ZONE, DropZoneEventType.CLICK, (event) => {
      if (!event?.payload || (event.payload as any).zoneId !== zoneId) return;

      // Find the first clickable child element (role="button" or button/a tag)
      const clickableChild = element.querySelector('[role="button"], button, a');

      if (clickableChild instanceof HTMLElement) {
        clickableChild.click();
      } else {
        // Fallback: click the element itself
        element.click();
      }
    });

    return () => unsubscribeClick();
  }, [enabled, targetRef, zoneId]);

  // Subscribe to mouse enter/leave events from overlay to trigger hover state
  useEffect(() => {
    if (!enabled || !targetRef.current) {
      return;
    }

    const element = targetRef.current;

    const unsubscribeMouseEnter = eventBus.subscribe(Module.DROP_ZONE, DropZoneEventType.MOUSE_ENTER, (event) => {
      if (!event?.payload || (event.payload as any).zoneId !== zoneId) return;

      element.classList.add(classesRef.current.hover);
    });

    const unsubscribeMouseLeave = eventBus.subscribe(Module.DROP_ZONE, DropZoneEventType.MOUSE_LEAVE, (event) => {
      if (!event?.payload || (event.payload as any).zoneId !== zoneId) return;

      element.classList.remove(classesRef.current.hover);
    });

    return () => {
      unsubscribeMouseEnter();
      unsubscribeMouseLeave();
      // Clean up hover class on unmount
      element.classList.remove(classesRef.current.hover);
    };
  }, [enabled, targetRef, zoneId]);

  // Subscribe to drop events
  useEffect(() => {
    const unsubscribe = eventBus.subscribe(Module.DROP_ZONE, DropZoneEventType.FILE_DROP, (event) => {
      if (!event) {
        return;
      }

      const data = event.payload as DropZoneFileDropData;

      if (!data) {
        console.error('[useDropZone] Event payload is undefined:', event);
        return;
      }

      if (!data.zoneId) {
        console.error('[useDropZone] Event payload missing zoneId:', data);
        return;
      }

      // Only handle drops for our zone
      if (data.zoneId !== zoneId) {
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
  }, [zoneId, targetRef]);

  // Unregister zone on unmount
  useEffect(() => {
    return () => {
      if (isRegisteredRef.current) {
        bridgeService.sendMessage({
          module: 'DROP_ZONE',
          type: 'UNREGISTER',
          payload: { zoneId }
        }).catch(err => {
          console.error('[useDropZone] Failed to unregister zone:', err);
        });
      }
    };
  }, [zoneId]);
}

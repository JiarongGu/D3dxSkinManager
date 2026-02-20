import { useRef, useEffect, useState } from 'react';
import './useDragDrop.css';

/**
 * Drop zone type - determines where the drop is allowed
 */
export type DropZoneType = 'node' | 'gap' | 'all';

/**
 * Drop position - calculated based on mouse position within target
 */
export type DropType = 'node' | 'gap';

export type DropGapPosition = 'top' | 'bottom';

/**
 * Configuration for a specific drag/drop event type
 */
export interface DragDropHandler {
  eventType: string; // For debugging purposes

  /**
   * CSS selector to find the drop target element
   * Example: '.ant-tree-node-content-wrapper' or '.tree-node'
   */
  nodeSelector?: string;

  /**
   * Where drops are allowed: 'node' (middle 70%), 'gap' (top/bottom 15%), or 'all'
   * Default: 'all'
   */
  allow?: DropZoneType;

  /**
   * Threshold for gap detection (percentage)
   * Default: 0.15 (15% top and bottom are "gap")
   */
  gapThreshold?: number;

  /**
   * Custom CSS class names for drag states
   * If not provided, defaults to 'drag-over-node' and 'drag-over-gap'
   */
  classes?: {
    node?: string;  // Class for "drop into" state (default: 'drag-over-node')
    gap?: string;   // Class for "drop between" state (default: 'drag-over-gap')
  };

  /**
   * Called when drop occurs
   * @param params - Drop event parameters
   * @param params.data - The data from dataTransfer.getData()
   * @param params.type - Where the drop occurred ('node' or 'gap')
   * @param params.gapPosition - If type is 'gap', whether it's 'top' or 'bottom'
   * @param params.target - The target element (result of nodeSelector.closest()) - extract additional data from here if needed
   * @param params.event - The original drop event (for advanced usage)
   */
  onDrop?: (params: {
    data?: string;
    type: DropType;
    gapPosition?: DropGapPosition;
    target: Element | null;
    event: DragEvent;
  }) => boolean | void;

  /**
   * Custom onDragOver handler (advanced usage)
   * If provided, this overrides the automatic styling logic
   */
  onDragOver?: (e: DragEvent) => boolean | void;

  /**
   * Custom onDragLeave handler (advanced usage)
   * If provided, this overrides the automatic cleanup logic
   */
  onDragLeave?: (e: DragEvent) => void;
}



/**
 * Generic drag and drop hook for custom drag/drop behavior
 *
 * This hook provides a clean, declarative way to handle multiple drag/drop
 * event types using native DOM events. Each event type (dataTransfer type)
 * gets its own handler configuration.
 *
 * @example Mod drops onto tree nodes
 * ```tsx
 * const { containerRef } = useDragDrop<HTMLDivElement>(
 *   {
 *     eventType: 'application/mod-sha',
 *     nodeSelector: '.ant-tree-node-content-wrapper',
 *     allow: 'node', // Only allow dropping into nodes, not between them
 *     onDrop: ({ data, target }) => {
 *       // data = mod SHA from dataTransfer
 *       // Extract node ID from DOM element
 *       const nodeId = target?.textContent?.trim().replace(/\s*\(\d+\)$/, '') || '';
 *       handleModDrop(data, nodeId);
 *       return true;
 *     }
 *   }
 * );
 * ```
 *
 * @example Tree node reorganization with gap detection
 * ```tsx
 * const { containerRef } = useDragDrop<HTMLDivElement>(
 *   {
 *     eventType: 'application/tree-node-id',
 *     nodeSelector: '.ant-tree-node-content-wrapper',
 *     allow: 'all', // Allow both node and gap drops
 *     gapThreshold: 0.15, // 15% top/bottom are gaps
 *     onDrop: ({ data, type, gapPosition, target }) => {
 *       const dropNodeId = target?.textContent?.trim().replace(/\s*\(\d+\)$/, '') || '';
 *       if (type === 'gap') {
 *         handleReorder(data, dropNodeId, gapPosition); // 'top' or 'bottom'
 *       } else {
 *         handleMakeChild(data, dropNodeId);
 *       }
 *       return true;
 *     }
 *   }
 * );
 * ```
 *
 * @example With custom CSS classes
 * ```tsx
 * useDragDrop({
 *   eventType: 'application/file',
 *   nodeSelector: '.file-item',
 *   classes: {
 *     node: 'file-drop-target',
 *     gap: 'file-drop-gap'
 *   },
 *   onDrop: ({ data, type }) => {
 *     console.log('Dropped file:', data);
 *     return true;
 *   }
 * });
 * ```
 *
 * @param handlers Drag/drop handler configurations
 * @returns Object with containerRef (attach to container element)
 */
export function useDragDrop<T extends HTMLElement = HTMLElement>(
  ...handlers: DragDropHandler[]
) {
  const [container, setContainer] = useState<T | null>(null);
  const handlersMapRef = useRef<Record<string, DragDropHandler>>(
    handlers.reduce<Record<string, DragDropHandler>>((map, handler) => {
      map[handler.eventType] = handler;
      return map;
    }, {})
  );

  // Update handlersMapRef whenever handlers change
  // This ensures that when callbacks are recreated with new data (e.g., tree updates),
  // the event listeners use the latest callbacks instead of stale closures
  useEffect(() => {
    handlersMapRef.current = handlers.reduce<Record<string, DragDropHandler>>((map, handler) => {
      map[handler.eventType] = handler;
      return map;
    }, {});
  }, [handlers]);

  useEffect(() => {
    if (!container) {
      return;
    }
    const handlerTypes = Object.keys(handlersMapRef.current);
    console.log('[useDragDrop] Registering event listeners', {
      container,
      handlerTypes,
      containerClass: container.className
    });

    /**
     * Calculate drop position based on mouse position within target
     */
    const calculateGapPosition = (
      e: DragEvent,
      target: Element,
      gapThreshold: number
    ): DropGapPosition => {
      const rect = target.getBoundingClientRect();
      const relativeY = (e.clientY - rect.top) / rect.height;

      if (relativeY < gapThreshold) {
        return 'top';
      }
      return 'bottom';
    };

    const calculateDropType = (
      e: DragEvent,
      target: Element,
      gapThreshold: number
    ): DropType => {
      const rect = target.getBoundingClientRect();
      const relativeY = (e.clientY - rect.top) / rect.height;

      // Check if in gap zone (top or bottom threshold)
      if (relativeY < gapThreshold || relativeY > (1 - gapThreshold)) {
        return 'gap';
      }
      return 'node';
    };

    /**
     * Unified dragover handler
     * Checks dataTransfer types and routes to appropriate handler
     */
    const handleDragOver = (e: DragEvent) => {
      const types = Array.from(e.dataTransfer?.types || []);

      // Find matching handler by checking if any type matches a handler key
      for (const type of types) {
        const handler = handlersMapRef.current[type];
        if (!handler) continue;

        // If custom onDragOver provided, use it
        if (handler.onDragOver) {
          const result = handler.onDragOver(e);
          if (result === true) {
            e.preventDefault();
            e.stopPropagation();
          }
          break;
        }

        // Otherwise, use automatic styling logic
        if (handler.nodeSelector) {
          const target = (e.target as HTMLElement).closest(handler.nodeSelector);
          if (target) {
            const gapThreshold = handler.gapThreshold ?? 0.15;
            const dropType = calculateDropType(e, target, gapThreshold);
            const allow = handler.allow ?? 'all';

            // Get custom or default class names
            const nodeClass = handler.classes?.node ?? 'drag-over-node';
            const gapClass = handler.classes?.gap ?? 'drag-over-gap';

            // Clean up all previous drag styling from all nodes in container
            // This ensures only the current target has styling
            const allNodes = container?.querySelectorAll(handler.nodeSelector);
            allNodes?.forEach(node => {
              node.classList.remove('drag-over-node', 'drag-over-gap', nodeClass, gapClass);
              node.removeAttribute('data-gap-position');
            });

            // Determine if drop is allowed
            const dropAllowed = (allow === 'all') || allow === dropType;

            // Add appropriate class based on position and allowed zones
            if (dropAllowed) {
              const className = dropType === 'node' ? nodeClass : gapClass;
              target.classList.add(className);

              // If it's a gap drop, set the gap position attribute for CSS styling
              if (dropType === 'gap') {
                const gapPosition = calculateGapPosition(e, target, gapThreshold);
                target.setAttribute('data-gap-position', gapPosition);
              }

              e.preventDefault();
              e.stopPropagation();
            }
          }
        }
        break;
      }
    };

    /**
     * Unified dragleave handler
     * Only clean up when actually leaving the element (not when entering child elements)
     */
    const handleDragLeave = (e: DragEvent) => {
      const types = Array.from(e.dataTransfer?.types || []);

      for (const type of types) {
        const handler = handlersMapRef.current[type];
        if (!handler) continue;

        // If custom onDragLeave provided, use it
        if (handler.onDragLeave) {
          handler.onDragLeave(e);
          break;
        }

        // Otherwise, use automatic cleanup logic
        if (handler.nodeSelector) {
          const target = (e.target as HTMLElement).closest(handler.nodeSelector);
          if (target) {
            // Only clean up if we're actually leaving the target element
            // (not just entering a child element)
            const relatedTarget = e.relatedTarget as HTMLElement;
            if (!relatedTarget || !target.contains(relatedTarget)) {
              // Remove both default and custom classes
              const nodeClass = handler.classes?.node ?? 'drag-over-node';
              const gapClass = handler.classes?.gap ?? 'drag-over-gap';
              target.classList.remove('drag-over-node', 'drag-over-gap', nodeClass, gapClass);
              target.removeAttribute('data-gap-position');
            }
          }
        }
        break;
      }
    };

    /**
     * Unified drop handler
     */
    const handleDrop = (e: DragEvent) => {
      const types = Array.from(e.dataTransfer?.types || []);
      console.log('[useDragDrop] handleDrop - dataTransfer types:', types, 'handlers:', Object.keys(handlersMapRef.current));

      for (const type of types) {
        const handler = handlersMapRef.current[type];
        if (!handler) {
          console.log(`[useDragDrop] No handler for type: ${type}`);
          continue;
        }
        console.log(`[useDragDrop] Processing handler for type: ${type}`);

        if (handler.onDrop) {
          console.log('[useDragDrop] handler.onDrop exists, processing drop...');
          // Extract data from dataTransfer using the event type
          let data = e.dataTransfer?.getData(type) || '';
          console.log('[useDragDrop] Extracted data from dataTransfer:', data);

          // Calculate position and target
          let dropType: DropType = 'node';
          let gapPosition: DropGapPosition | undefined = undefined;
          let target: Element | null = null;

          if (handler.nodeSelector) {
            // Try to find target from event target first
            target = (e.target as HTMLElement).closest(handler.nodeSelector);

            // Debug logging
            if (!target) {
              console.log('[useDragDrop] No target from e.target.closest(), trying fallbacks...', {
                eventTargetTag: (e.target as HTMLElement).tagName,
                eventTargetClass: (e.target as HTMLElement).className,
                mousePos: { x: e.clientX, y: e.clientY },
                containerExists: !!container
              });
            }

            // If no target found, try multiple fallback strategies
            if (!target) {
              // Strategy 1: Check if e.target is inside the container, search from there
              const eventTarget = e.target as HTMLElement;
              if (container && container.contains(eventTarget)) {
                // Search up from the actual event target within our container
                let current: HTMLElement | null = eventTarget;
                while (current && current !== container) {
                  const found = current.querySelector(handler.nodeSelector);
                  if (found) {
                    target = found;
                    break;
                  }
                  // Also check if current matches the selector
                  if (current.matches(handler.nodeSelector)) {
                    target = current;
                    break;
                  }
                  current = current.parentElement;
                }
              }

              // Strategy 2: Use mouse position as last resort
              if (!target) {
                const elementAtPoint = document.elementFromPoint(e.clientX, e.clientY);
                if (elementAtPoint) {
                  target = elementAtPoint.closest(handler.nodeSelector);
                }
              }

              // Strategy 3: If still no target, search within container at mouse position
              if (!target && container) {
                const allNodes = container.querySelectorAll(handler.nodeSelector);
                for (const node of Array.from(allNodes)) {
                  const rect = node.getBoundingClientRect();
                  if (
                    e.clientX >= rect.left &&
                    e.clientX <= rect.right &&
                    e.clientY >= rect.top &&
                    e.clientY <= rect.bottom
                  ) {
                    target = node;
                    break;
                  }
                }
              }

              // Log which strategy worked (if any)
              if (target) {
                console.log('[useDragDrop] Found target using fallback strategy:', {
                  targetElement: target,
                  targetClass: target.className
                });
              } else {
                console.error('[useDragDrop] All fallback strategies failed to find target');
              }
            }

            if (target) {
              const gapThreshold = handler.gapThreshold ?? 0.15;
              dropType = calculateDropType(e, target, gapThreshold);

              // If it's a gap drop, calculate which side
              if (dropType === 'gap') {
                gapPosition = calculateGapPosition(e, target, gapThreshold);
              }

              // Clean up visual feedback (both default and custom classes)
              const nodeClass = handler.classes?.node ?? 'drag-over-node';
              const gapClass = handler.classes?.gap ?? 'drag-over-gap';
              target.classList.remove('drag-over-node', 'drag-over-gap', nodeClass, gapClass);
              target.removeAttribute('data-gap-position');
            }
          }

          console.log('[useDragDrop] About to call handler.onDrop with:', { data, dropType, gapPosition, target });
          const result = handler.onDrop({ data, type: dropType, gapPosition, target, event: e });
          console.log('[useDragDrop] handler.onDrop returned:', result);
          if (result === true) {
            e.preventDefault();
            e.stopPropagation();
          }
          break;
        }
      }
    };

    // Register event listeners
    container.addEventListener('dragover', handleDragOver);
    container.addEventListener('dragleave', handleDragLeave);
    container.addEventListener('drop', handleDrop);

    return () => {
      console.log('[useDragDrop] Removing event listeners', {
        container,
        containerClass: container.className
      });
      container.removeEventListener('dragover', handleDragOver);
      container.removeEventListener('dragleave', handleDragLeave);
      container.removeEventListener('drop', handleDrop);
    };
  }, [container]); // Re-run when container changes

  return {
    /**
     * Callback ref to attach to the container element
     * Usage: <div ref={containerRef}>...</div>
     */
    containerRef: setContainer,
  };
}

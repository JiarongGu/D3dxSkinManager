import { useEffect, useRef, useCallback } from 'react';
import { bridgeService } from '../services/bridgeService';
import { eventBus, DropZoneEventType, Module } from '../services/eventBus';
import { v4 as uuidv4 } from 'uuid';
import { debounce } from 'lodash-es';
import './useDropZone.css';
import logger from '../utils/logger';

export interface DropZoneFileDropData {
  zoneId: string;
  files: string[];
  position: { x: number; y: number };
}

/**
 * Creates a WinForms drop zone overlay that syncs with a web element
 *
 * How it works:
 * - Frontend sets data-drop-zone-id on element and sends bounds to backend
 * - Backend creates overlay at specified position, tracks mouse/drag state
 * - Backend uses ExecuteScriptAsync to check DOM occlusion when file drag enters
 * - Overlay visibility: mouse outside OR file dragging (unless occluded)
 *
 * Features:
 * - Captures real OS file paths (not blob URLs)
 * - Auto-syncs position on resize/scroll
 * - CSS class for visual feedback on drag-over
 */
export function useDropZone(options: {
  targetRef: React.RefObject<HTMLElement | null>;
  onDrop: (files: string[]) => void;
  enabled?: boolean;
  zoneId?: string;
  className?: string;
}) {
  const { targetRef, onDrop, enabled = true, zoneId: customZoneId, className = 'use-drop-zone-drop' } = options;

  const zoneIdRef = useRef(customZoneId || `drop-zone-${uuidv4()}`);
  const dropClassRef = useRef(className);
  const onDropRef = useRef(onDrop);
  const isRegisteredRef = useRef(false);
  const lastBoundsRef = useRef({ x: 0, y: 0, width: 0, height: 0 });
  const mouseInsideRef = useRef(false);

  useEffect(() => {
    onDropRef.current = onDrop;
  }, [onDrop]);

  const updateZoneBoundsImmediate = useCallback(() => {
    if (!targetRef.current) return;

    const rect = targetRef.current.getBoundingClientRect();
    const bounds = {
      zoneId: zoneIdRef.current,
      x: Math.round(rect.left),
      y: Math.round(rect.top),
      width: Math.round(rect.width),
      height: Math.round(rect.height)
    };

    const boundsChanged =
      !isRegisteredRef.current ||
      bounds.x !== lastBoundsRef.current.x ||
      bounds.y !== lastBoundsRef.current.y ||
      bounds.width !== lastBoundsRef.current.width ||
      bounds.height !== lastBoundsRef.current.height;

    if (!isRegisteredRef.current) {
      lastBoundsRef.current = bounds;
      logger.debug(`[useDropZone] Registering zone: ${zoneIdRef.current}`);
      bridgeService.sendMessage({
        module: 'DROP_ZONE',
        type: 'REGISTER',
        payload: bounds
      }).then(() => {
        isRegisteredRef.current = true;
      }).catch(err => {
        logger.error('[useDropZone] Failed to register zone:', err);
      });
    } else if (boundsChanged) {
      lastBoundsRef.current = bounds;
      logger.verbose(`[useDropZone] Updating zone bounds: ${zoneIdRef.current}`);
      bridgeService.sendMessage({
        module: 'DROP_ZONE',
        type: 'UPDATE',
        payload: bounds
      }).catch(err => {
        logger.error('[useDropZone] Failed to update zone:', err);
      });
    }
  }, [targetRef]);

  const updateZoneBounds = useRef(debounce(updateZoneBoundsImmediate, 100)).current;

  // Track element position and register/update zone
  useEffect(() => {
    if (!enabled || !targetRef.current) return;

    const element = targetRef.current;

    element.setAttribute('data-drop-zone-id', zoneIdRef.current);
    updateZoneBounds();

    // Track mouse globally to detect when it leaves zone bounds (debounced for performance)
    const handleGlobalMouseMoveImmediate = (e: MouseEvent) => {
      const rect = element.getBoundingClientRect();
      const isInside =
        e.clientX >= rect.left &&
        e.clientX <= rect.right &&
        e.clientY >= rect.top &&
        e.clientY <= rect.bottom;

      // Only send SHOW when transitioning from inside to outside
      if (mouseInsideRef.current && !isInside) {
        mouseInsideRef.current = false;
        bridgeService.sendMessage({
          module: 'DROP_ZONE',
          type: 'SHOW',
          payload: { zoneId: zoneIdRef.current }
        }).catch(err => {
          logger.error('[useDropZone] Failed to send SHOW:', err);
        });
      } else if (!mouseInsideRef.current && isInside) {
        mouseInsideRef.current = true;
      }
    };

    const handleGlobalMouseMove = debounce(handleGlobalMouseMoveImmediate, 100);

    // Show overlay when document loses focus (mousemove stops firing)
    const handleDocumentBlur = () => {
      mouseInsideRef.current = false;
      bridgeService.sendMessage({
        module: 'DROP_ZONE',
        type: 'SHOW',
        payload: { zoneId: zoneIdRef.current }
      }).catch(err => {
        logger.error('[useDropZone] Failed to send SHOW on blur:', err);
      });
    };

    document.addEventListener('mousemove', handleGlobalMouseMove);
    window.addEventListener('blur', handleDocumentBlur);

    const resizeObserver = new ResizeObserver(() => updateZoneBounds());
    const intersectionObserver = new IntersectionObserver(() => updateZoneBounds(), {
      threshold: [0, 0.1, 0.5, 0.9, 1.0]
    });

    resizeObserver.observe(element);
    intersectionObserver.observe(element);
    window.addEventListener('scroll', updateZoneBounds, true);
    window.addEventListener('resize', updateZoneBounds);

    return () => {
      updateZoneBounds.cancel();
      handleGlobalMouseMove.cancel();
      resizeObserver.disconnect();
      intersectionObserver.disconnect();
      window.removeEventListener('scroll', updateZoneBounds, true);
      window.removeEventListener('resize', updateZoneBounds);
      window.removeEventListener('blur', handleDocumentBlur);
      document.removeEventListener('mousemove', handleGlobalMouseMove);
      element.removeAttribute('data-drop-zone-id');
    };
  }, [enabled, targetRef, updateZoneBounds]);

  // Handle drag enter/leave for visual feedback
  useEffect(() => {
    if (!enabled || !targetRef.current) return;

    const element = targetRef.current;

    const handleDragEnter = (event: any) => {
      if (event?.payload?.zoneId === zoneIdRef.current) {
        element.classList.add(dropClassRef.current);
      }
    };

    const handleDragLeave = (event: any) => {
      if (event?.payload?.zoneId === zoneIdRef.current) {
        element.classList.remove(dropClassRef.current);
      }
    };

    const unsubscribeDragEnter = eventBus.subscribe(Module.DROP_ZONE, DropZoneEventType.DRAG_ENTER, handleDragEnter);
    const unsubscribeDragLeave = eventBus.subscribe(Module.DROP_ZONE, DropZoneEventType.DRAG_LEAVE, handleDragLeave);

    return () => {
      unsubscribeDragEnter();
      unsubscribeDragLeave();
      element.classList.remove(dropClassRef.current);
    };
  }, [enabled, targetRef]);

  // Handle file drops
  useEffect(() => {
    const handleFileDrop = (event: any) => {
      if (!event) return;

      const data = event.payload as DropZoneFileDropData;
      if (!data?.zoneId || data.zoneId !== zoneIdRef.current) return;

      if (targetRef.current) {
        targetRef.current.classList.remove(dropClassRef.current);
      }

      onDropRef.current(data.files);
    };

    const unsubscribe = eventBus.subscribe(Module.DROP_ZONE, DropZoneEventType.FILE_DROP, handleFileDrop);
    return () => unsubscribe();
  }, [targetRef]);

  // Unregister on unmount
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

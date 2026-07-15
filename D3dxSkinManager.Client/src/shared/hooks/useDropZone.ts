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
 * 1. Mouse leaves area -> Frontend sends -> SHOW Overlay visible
 * 2. Mouse enters area -> Backend checks occlusion -> Hides overlay (unless dragging files)
 * 3. Form inactive -> Show overlay (for drag-drop from other apps)
 * 4. Form active -> Frontend handles via (1)
 * 5. When overlay visible -> Backend sends MOUSE_ENTER/LEAVE events for CSS styles
 *
 * Features:
 * - Captures real OS file paths (not blob URLs)
 * - Auto-syncs position on resize/scroll
 * - CSS classes for hover and drag-over feedback
 * - Works even when form is in background
 */
export function useDropZone(options: {
  targetRef: React.RefObject<HTMLElement | null>;
  onDrop: (files: string[]) => void;
  enabled?: boolean;
  zoneId?: string;
  className?: string;
}) {
  const { targetRef, onDrop, enabled = true, zoneId: customZoneId, className } = options;

  const zoneIdRef = useRef(customZoneId || `drop-zone-${uuidv4()}`);
  const dropClassRef = useRef(className || 'use-drop-zone-drop'); // Default drop class
  const onDropRef = useRef(onDrop);
  const isRegisteredRef = useRef(false);
  // Whether a REGISTER has ever been SENT for this zone (even if not yet acked). The cleanup unregisters
  // on THIS (not on the ack) so a fast unmount before REGISTER resolves still tears the overlay down (F3).
  const attemptedRef = useRef(false);
  // A REGISTER is currently in flight — guards against sending a duplicate before the first resolves (F5).
  const registeringRef = useRef(false);
  const lastBoundsRef = useRef({ x: 0, y: 0, width: 0, height: 0 });

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
      if (registeringRef.current) return; // a REGISTER is already in flight — don't send a duplicate (F5)
      registeringRef.current = true;
      attemptedRef.current = true;
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
      }).finally(() => {
        registeringRef.current = false;
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

    // IDred debounced function to send SHOW message
    const sendShowMessageImmediate = () => {
      bridgeService.sendMessage({
        module: 'DROP_ZONE',
        type: 'SHOW',
        payload: { zoneId: zoneIdRef.current }
      }).catch(err => {
        logger.error('[useDropZone] Failed to send SHOW:', err);
      });
    };

    const sendShowMessage = debounce(sendShowMessageImmediate, 100);

    // Use native element mouseleave event - send SHOW when mouse leaves
    const handleElementMouseLeave = () => {
      sendShowMessage();
    };

    // Show overlay when document loses focus
    const handleDocumentBlur = () => {
      sendShowMessage();
    };

    element.addEventListener('mouseleave', handleElementMouseLeave);
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
      sendShowMessage.cancel();
      updateZoneBounds.cancel();
      resizeObserver.disconnect();
      intersectionObserver.disconnect();
      window.removeEventListener('scroll', updateZoneBounds, true);
      window.removeEventListener('resize', updateZoneBounds);
      window.removeEventListener('blur', handleDocumentBlur);
      element.removeEventListener('mouseleave', handleElementMouseLeave);
      element.removeAttribute('data-drop-zone-id');

      // Unregister whenever this effect tears down — on unmount OR when `enabled` flips false (F4) —
      // unconditionally (not gated on the REGISTER ack) so an in-flight REGISTER is also torn down (F3).
      // The backend UnregisterZone no-ops if the overlay isn't there yet, and the ordered IPC channel
      // processes our earlier REGISTER before this UNREGISTER (create-then-destroy, no orphan).
      if (attemptedRef.current) {
        bridgeService.sendMessage({
          module: 'DROP_ZONE',
          type: 'UNREGISTER',
          payload: { zoneId: zoneIdRef.current },
        }).catch(err => {
          logger.error('[useDropZone] Failed to unregister zone:', err);
        });
        isRegisteredRef.current = false;
        attemptedRef.current = false;
      }
    };
  }, [enabled, targetRef, updateZoneBounds]);

  // Handle drag enter/leave for drop CSS effects
  useEffect(() => {
    if (!enabled || !targetRef.current || !dropClassRef.current) return;

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

}

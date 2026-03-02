import { useEffect } from 'react';
import {
  eventBus,
  Module,
  Event,
  ModuleEventTypeMap,
} from '../services/eventBus';

/**
 * Hook for subscribing to backend events with proper typing
 * Automatically manages subscription lifecycle
 * Unwraps event payload for convenience
 *
 * STRONGLY TYPED:
 * - eventType must match the Module (e.g., ModEventType for Module.MOD)
 * - No string literals allowed
 * - TypeScript enforces correct module + event type combinations
 *
 * @example
 * ```tsx
 * // âœ?CORRECT: Module.MOD requires ModEventType
 * useEventSubscription(Module.MOD, ModEventType.REFRESHED, () => {
 *    *   loadMods();
 * });
 *
 * // âœ?With payload - type-safe!
 * useEventSubscription(Module.TASK_QUEUE, TaskQueueEventType.PROGRESS, (progress) => {
 *    * });
 *
 * // â?WRONG: String literals not allowed
 * useEventSubscription(Module.MOD, 'REFRESHED', () => { ... }); // TS Error!
 *
 * // â?WRONG: Mismatched module and event type
 * useEventSubscription(Module.MOD, TaskQueueEventType.PROGRESS, () => { ... }); // TS Error!
 * ```
 */
export function useEventSubscription<
  M extends Module,
  T extends ModuleEventTypeMap[M],
  E extends Event<M, T> = Event<M, T>
>(
  module: M,
  eventType: T,
  handler: (payload: E['payload']) => void,
  deps: React.DependencyList = []
): void {
  useEffect(() => {
    // Wrap handler to unwrap event.payload
    const wrappedHandler = (event: Event<M, T>) => {
      handler(event.payload as E['payload']);
    };

    const unsubscribe = eventBus.subscribe(module, eventType, wrappedHandler);

    return unsubscribe;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [module, eventType, ...deps]);
}

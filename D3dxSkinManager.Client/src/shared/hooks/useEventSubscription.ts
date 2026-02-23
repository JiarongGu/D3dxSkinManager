import { useEffect } from 'react';
import { eventBus, EventType } from '../services/eventBus';

/**
 * Hook for subscribing to backend events
 * Automatically manages subscription lifecycle
 *
 * @example
 * ```tsx
 * useEventSubscription(EventType.ModsRefreshed, () => {
 *   console.log('Mods were refreshed!');
 *   loadMods();
 * });
 * ```
 */
export function useEventSubscription<T = unknown>(
  eventType: EventType | string,
  handler: (data: T | undefined) => void,
  deps: React.DependencyList = []
): void {
  useEffect(() => {
    const subscription = eventBus.subscribe<T>(eventType, handler);

    return () => {
      subscription.unsubscribe();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [eventType, ...deps]);
}

/**
 * Hook for subscribing to custom backend events
 *
 * @example
 * ```tsx
 * useCustomEventSubscription('migration.completed', (result) => {
 *   console.log('Migration completed:', result);
 * });
 * ```
 */
export function useCustomEventSubscription<T = unknown>(
  eventName: string,
  handler: (data: T | undefined) => void,
  deps: React.DependencyList = []
): void {
  useEffect(() => {
    const subscription = eventBus.subscribeToCustomEvent<T>(eventName, handler);

    return () => {
      subscription.unsubscribe();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [eventName, ...deps]);
}

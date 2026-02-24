import { useEffect } from 'react';
import { eventBus, EventType, Event } from '../services/eventBus';

/**
 * Hook for subscribing to backend events
 * Automatically manages subscription lifecycle
 * Unwraps event data for convenience
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
    // Wrap handler to unwrap event.data
    const wrappedHandler = (event: Event<T> | undefined) => {
      handler(event?.data);
    };

    const subscription = eventBus.subscribe<Event<T>>(eventType, wrappedHandler);

    return () => {
      subscription.unsubscribe();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [eventType, ...deps]);
}

/**
 * Hook for subscribing to custom backend events
 * Unwraps event data for convenience
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
    // Wrap handler to unwrap event.data
    const wrappedHandler = (event: Event<T> | undefined) => {
      handler(event?.data);
    };

    const subscription = eventBus.subscribeToCustomEvent<Event<T>>(eventName, wrappedHandler);

    return () => {
      subscription.unsubscribe();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [eventName, ...deps]);
}

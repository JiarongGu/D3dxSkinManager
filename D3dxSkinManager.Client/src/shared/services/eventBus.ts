/**
 * Event Bus - Centralized event subscription pattern for backend-to-frontend events
 * Supports typed events with RxJS-like subscription model
 */

// Event type enum matching backend EventType
// All events use SCREAMING_SNAKE_CASE for consistency with IPC notification format
export enum EventType {
  ApplicationStarted = 'APPLICATION_STARTED',
  ApplicationShutdown = 'APPLICATION_SHUTDOWN',
  ModLoaded = 'MOD_LOADED',
  ModUnloaded = 'MOD_UNLOADED',
  ModDeleted = 'MOD_DELETED',
  ModImported = 'MOD_IMPORTED',
  ModsRefreshed = 'MODS_REFRESHED',
  ClassificationTreeChanged = 'CLASSIFICATION_TREE_CHANGED',
  CustomEvent = 'CUSTOM_EVENT',
  LogLevelChanged = 'LOG_LEVEL_CHANGED',
  // Drop zone events
  DropZoneClick = 'DROP_ZONE_CLICK',
  DropZoneDragEnter = 'DROP_ZONE_DRAG_ENTER',
  DropZoneDragLeave = 'DROP_ZONE_DRAG_LEAVE',
  DropZoneFileDrop = 'DROP_ZONE_FILE_DROP',
  DropZoneMouseEnter = 'DROP_ZONE_MOUSE_ENTER',
  DropZoneMouseLeave = 'DROP_ZONE_MOUSE_LEAVE',
}

// Event payload types
export interface Event<T = unknown> {
  type: EventType;
  eventName?: string; // For CustomEvent
  data?: T;
}

// Subscription handler
type EventHandler<T = unknown> = (data: T | undefined) => void;

interface Subscription {
  unsubscribe: () => void;
}

/**
 * Event Bus for backend-to-frontend event streaming
 */
class EventBus {
  private handlers: Map<string, Set<EventHandler>> = new Map();

  /**
   * Subscribe to a specific event type
   * Returns subscription object with unsubscribe method
   */
  subscribe<T = unknown>(
    eventType: EventType | string,
    handler: EventHandler<T>
  ): Subscription {
    const key = this.getEventKey(eventType);

    if (!this.handlers.has(key)) {
      this.handlers.set(key, new Set());
    }

    this.handlers.get(key)!.add(handler as EventHandler);

    // Return subscription object
    return {
      unsubscribe: () => {
        const handlers = this.handlers.get(key);
        if (handlers) {
          handlers.delete(handler as EventHandler);
          if (handlers.size === 0) {
            this.handlers.delete(key);
          }
        }
      },
    };
  }

  /**
   * Subscribe to a custom event by name
   */
  subscribeToCustomEvent<T = unknown>(
    eventName: string,
    handler: EventHandler<T>
  ): Subscription {
    return this.subscribe(`${EventType.CustomEvent}:${eventName}`, handler);
  }

  /**
   * Emit an event to all subscribers
   * Called by bridge service when receiving backend events
   */
  emit<T = unknown>(event: Event<T>): void {
    const key = this.getEventKey(event.type, event.eventName);
    const handlers = this.handlers.get(key);

    if (handlers && handlers.size > 0) {
      handlers.forEach((handler) => {
        try {
          handler(event);
        } catch (error) {
          console.error(`Error in event handler for ${key}:`, error);
        }
      });
    }
  }

  /**
   * Get the key for storing handlers
   */
  private getEventKey(
    eventType: EventType | string,
    eventName?: string
  ): string {
    if (eventType === EventType.CustomEvent && eventName) {
      return `${EventType.CustomEvent}:${eventName}`;
    }
    return String(eventType);
  }

  /**
   * Clear all subscriptions (useful for testing)
   */
  clear(): void {
    this.handlers.clear();
  }

  /**
   * Get subscription count for debugging
   */
  getSubscriptionCount(eventType?: EventType | string): number {
    if (eventType) {
      const key = this.getEventKey(eventType);
      return this.handlers.get(key)?.size || 0;
    }

    let total = 0;
    this.handlers.forEach((handlers) => {
      total += handlers.size;
    });
    return total;
  }

  /**
   * Convenience method for subscribing to events
   * Returns a function that unsubscribes when called
   */
  on<T = unknown>(
    eventType: EventType | string,
    handler: EventHandler<Event<T>>
  ): () => void {
    const subscription = this.subscribe(eventType, handler as EventHandler);
    return () => subscription.unsubscribe();
  }
}

// Export singleton instance
export const eventBus = new EventBus();

/**
 * Event Bus - Centralized event subscription pattern for backend-to-frontend events
 * Supports typed events with compile-time safety
 */

import type { MigrationProgress, MigrationResult } from "../types/migration.types";
import type { WorkflowInfo } from "../../modules/workflow/types/workflow.types";
import type { ModInfo } from "../types/mod.types";
import { bridgeService } from "./bridgeService";

// Module names matching backend ModuleNames
export enum Module {
  APP = "APP",
  SYSTEM = "SYSTEM",
  MOD = "MOD",
  CATEGORY = "CATEGORY",
  WORKFLOW = "WORKFLOW",
  DROP_ZONE = "DROP_ZONE",
  SETTING = "SETTING",
  PROFILE = "PROFILE",
  MIGRATION = "MIGRATION",
  TOOL = "TOOL",
  PLUGIN = "PLUGIN",
}

// Core event types
export enum SystemEventType {
  APPLICATION_STARTED = "APPLICATION_STARTED",
  APPLICATION_SHUTDOWN = "APPLICATION_SHUTDOWN",
  LOG_LEVEL_CHANGED = "LOG_LEVEL_CHANGED",
}

// Mod event types
export enum ModEventType {
  LOADED = "LOADED",
  UNLOADED = "UNLOADED",
  DELETED = "DELETED",
  IMPORTED = "IMPORTED",
  REFRESHED = "REFRESHED",
  METADATA_UPDATED = "METADATA_UPDATED",
  CATEGORY_UPDATED = "CATEGORY_UPDATED",
  PREVIEW_IMPORTED = "PREVIEW_IMPORTED",
  THUMBNAIL_UPDATED = "THUMBNAIL_UPDATED",
  PREVIEW_DELETED = "PREVIEW_DELETED",
}

// Category event types
export enum CategoryEventType {
  CATEGORY_TREE_UPDATED = "CATEGORY_TREE_UPDATED",
}

// Drop zone event types
export enum DropZoneEventType {
  DRAG_ENTER = "DRAG_ENTER",
  DRAG_LEAVE = "DRAG_LEAVE",
  FILE_DROP = "FILE_DROP",
  MOUSE_ENTER = "MOUSE_ENTER",
  MOUSE_LEAVE = "MOUSE_LEAVE",
}

// Workflow event types
export enum WorkflowEventType {
  CREATED = "CREATED",
  STATUS_CHANGED = "STATUS_CHANGED",
  COMPLETED = "COMPLETED",
  FAILED = "FAILED",
  CANCELLED = "CANCELLED",
  PROGRESS = "PROGRESS",
  DELETED = "DELETED",
}

// Settings event types
export enum SettingsEventType {
  WINDOW_STATE_RESET = "WINDOW_STATE_RESET",
  GLOBAL_SETTINGS_CHANGED = "GLOBAL_SETTINGS_CHANGED",
}

// Profile event types
export enum ProfileEventType {
  CREATED = "CREATED",
  UPDATED = "UPDATED",
  DELETED = "DELETED",
  DUPLICATED = "DUPLICATED",
  SWITCHED = "SWITCHED",
  CONFIG_UPDATED = "CONFIG_UPDATED",
}

// Migration event types
export enum MigrationEventType {
  PROGRESS = "PROGRESS",
  COMPLETED = "COMPLETED",
}

// Tools event types
export enum ToolsEventType {
  CACHE_CLEANED = "CACHE_CLEANED",
  CACHE_ITEM_DELETED = "CACHE_ITEM_DELETED",
}

// NOTE: Plugin events are NOT currently used in backend - reserved for future cross-plugin communication
export type PluginEventType = string;

// Map each module to its valid event type enum
export interface ModuleEventTypeMap {
  [Module.APP]: never; // No events currently emitted from APP module
  [Module.SYSTEM]: SystemEventType;
  [Module.MOD]: ModEventType;
  [Module.CATEGORY]: CategoryEventType;
  [Module.DROP_ZONE]: DropZoneEventType;
  [Module.WORKFLOW]: WorkflowEventType;
  [Module.SETTING]: SettingsEventType;
  [Module.PROFILE]: ProfileEventType;
  [Module.MIGRATION]: MigrationEventType;
  [Module.TOOL]: ToolsEventType;
  [Module.PLUGIN]: PluginEventType;
}

// Event payload type mapping
export interface EventPayloadMap {
  // System events
  [Module.SYSTEM]: {
    [SystemEventType.APPLICATION_STARTED]: void;
    [SystemEventType.APPLICATION_SHUTDOWN]: void;
    [SystemEventType.LOG_LEVEL_CHANGED]: { level: string };
  };

  // Mod events
  [Module.MOD]: {
    [ModEventType.LOADED]: { sha: string };
    [ModEventType.UNLOADED]: { sha: string };
    [ModEventType.DELETED]: { sha: string; mod?: ModInfo };
    [ModEventType.IMPORTED]: ModInfo;
    [ModEventType.REFRESHED]: void;
    [ModEventType.METADATA_UPDATED]: { sha: string; mod?: ModInfo };
    [ModEventType.CATEGORY_UPDATED]: {
      sha: string;
      category: string;
      mod?: ModInfo;
    };
    [ModEventType.PREVIEW_IMPORTED]: { sha: string; imagePath?: string };
    [ModEventType.THUMBNAIL_UPDATED]: { sha: string; previewPath: string };
    [ModEventType.PREVIEW_DELETED]: { sha: string; previewPath: string };
  };

  // Category events
  [Module.CATEGORY]: {
    [CategoryEventType.CATEGORY_TREE_UPDATED]: unknown;
  };

  // Drop zone events
  [Module.DROP_ZONE]: {
    [DropZoneEventType.DRAG_ENTER]: { zoneId: string };
    [DropZoneEventType.DRAG_LEAVE]: { zoneId: string };
    [DropZoneEventType.FILE_DROP]: { zoneId: string; files: string[] };
    [DropZoneEventType.MOUSE_ENTER]: { zoneId: string };
    [DropZoneEventType.MOUSE_LEAVE]: { zoneId: string };
  };

  // Workflow events
  [Module.WORKFLOW]: {
    [WorkflowEventType.CREATED]: WorkflowInfo;
    [WorkflowEventType.STATUS_CHANGED]: WorkflowInfo;
    [WorkflowEventType.COMPLETED]: WorkflowInfo;
    [WorkflowEventType.FAILED]: WorkflowInfo;
    [WorkflowEventType.CANCELLED]: WorkflowInfo;
    [WorkflowEventType.PROGRESS]: { workflowId: string; progress: number; step: string };
  };

  // Settings events
  [Module.SETTING]: {
    [SettingsEventType.WINDOW_STATE_RESET]: void;
    [SettingsEventType.GLOBAL_SETTINGS_CHANGED]: unknown;
  };

  // Profile events
  [Module.PROFILE]: {
    [ProfileEventType.CREATED]: unknown;
    [ProfileEventType.UPDATED]: unknown;
    [ProfileEventType.DELETED]: unknown;
    [ProfileEventType.DUPLICATED]: unknown;
    [ProfileEventType.SWITCHED]: { profileId: string };
    [ProfileEventType.CONFIG_UPDATED]: unknown;
  };

  // Migration events
  [Module.MIGRATION]: {
    [MigrationEventType.PROGRESS]: MigrationProgress;
    [MigrationEventType.COMPLETED]: MigrationResult;
  };

  // Tools events
  [Module.TOOL]: {
    [ToolsEventType.CACHE_CLEANED]: unknown;
    [ToolsEventType.CACHE_ITEM_DELETED]: { key: string };
  };

  // Plugins events (not currently used)
  [Module.PLUGIN]: Record<string, never>;
}

// Generic event structure
export interface Event<M extends Module = Module, T extends string = string> {
  module: M;
  type: T;
  payload?: M extends keyof EventPayloadMap
    ? T extends keyof EventPayloadMap[M]
      ? EventPayloadMap[M][T]
      : unknown
    : unknown;
}

// Subscription handler - receives typed Event
type EventHandler<M extends Module, T extends string> = (
  event: Event<M, T>,
) => void;

/**
 * Event Bus for backend-to-frontend event streaming
 */
class EventBus {
  private handlers: Map<string, Set<EventHandler<any, any>>> = new Map();
  private pendingUnsubscribes: Map<string, NodeJS.Timeout> = new Map();
  private readonly UNSUBSCRIBE_DEBOUNCE_MS = 10;

  /**
   * Subscribe to specific module and event type
   * Handler receives typed Event with payload property
   * Returns cleanup function for use in React useEffect
   */
  subscribe<M extends Module, T extends string>(
    module: M,
    type: T,
    handler: EventHandler<M, T>,
  ): () => void {
    const key = this.getEventKey(module, type);

    // Cancel any pending unsubscribe for this event
    const pendingUnsubscribe = this.pendingUnsubscribes.get(key);
    if (pendingUnsubscribe) {
      clearTimeout(pendingUnsubscribe);
      this.pendingUnsubscribes.delete(key);
    }

    let handlers = this.handlers.get(key);
    const isFirstSubscriber = !handlers || handlers.size === 0;

    if (!handlers) {
      handlers = new Set();
      this.handlers.set(key, handlers);
    }

    handlers.add(handler);

    // Only send SUBSCRIBE to backend if this is the first subscriber
    if (isFirstSubscriber) {
      bridgeService.sendMessage({
        module: "APP",
        type: "SUBSCRIBE",
        payload: { module, type },
      });
    }

    // Return cleanup function
    return () => {
      const handlers = this.handlers.get(key);
      if (handlers) {
        handlers.delete(handler);

        // If no more handlers, schedule unsubscribe with debounce
        if (handlers.size === 0) {
          this.handlers.delete(key);

          // Cancel any existing pending unsubscribe
          const existing = this.pendingUnsubscribes.get(key);
          if (existing) {
            clearTimeout(existing);
          }

          // Schedule unsubscribe with 50ms debounce
          // If someone resubscribes within 50ms, this will be cancelled
          const timeoutId = setTimeout(() => {
            this.pendingUnsubscribes.delete(key);
            bridgeService.sendMessage({
              module: "APP",
              type: "UNSUBSCRIBE",
              payload: { module, type },
            });
          }, this.UNSUBSCRIBE_DEBOUNCE_MS);

          this.pendingUnsubscribes.set(key, timeoutId);
        }
      }
    };
  }

  /**
   * Emit an event to all subscribers
   * Called by bridge service when receiving backend events
   */
  emit<M extends Module, T extends string>(event: Event<M, T>): void {
    const key = this.getEventKey(event.module, event.type);
    const handlers = this.handlers.get(key);

    if (handlers && handlers.size > 0) {
      handlers.forEach((handler) => {
        try {
          handler(event);
        } catch (error) {
                  }
      });
    }
  }

  /**
   * Get the key for storing handlers
   * Format: "MODULE.TYPE"
   */
  private getEventKey(module: string, type: string): string {
    return `${module}.${type}`;
  }

  /**
   * Clear all subscriptions (useful for testing)
   */
  clear(): void {
    // Clear all pending unsubscribe timeouts
    this.pendingUnsubscribes.forEach((timeoutId) => clearTimeout(timeoutId));
    this.pendingUnsubscribes.clear();
    this.handlers.clear();
  }

  /**
   * Get subscription count for debugging
   */
  getSubscriptionCount(module?: string, type?: string): number {
    if (module && type) {
      const key = this.getEventKey(module, type);
      return this.handlers.get(key)?.size || 0;
    }

    let total = 0;
    this.handlers.forEach((handlers) => {
      total += handlers.size;
    });
    return total;
  }
}

// Export singleton instance
export const eventBus = new EventBus();

// Combined EventType for backward compatibility
export const EventType = {
  ...SystemEventType,
  ...ModEventType,
  ...DropZoneEventType,
  ...WorkflowEventType,
  ...SettingsEventType,
  ...ProfileEventType,
  ...MigrationEventType,
  ...ToolsEventType,
  // PluginEventType is just 'string' now - no enum values to spread
} as const;

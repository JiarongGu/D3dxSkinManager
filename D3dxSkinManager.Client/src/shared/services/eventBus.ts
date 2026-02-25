/**
 * Event Bus - Centralized event subscription pattern for backend-to-frontend events
 * Supports typed events with compile-time safety
 */

import type { TaskInfo, TaskProgress } from '../../modules/taskQueue/types/task.types';
import type { ModInfo } from '../types/mod.types';

// Module names matching backend ModuleNames
export enum Module {
  CORE = 'CORE',
  MOD = 'MOD',
  TASK_QUEUE = 'TASK_QUEUE',
  DROP_ZONE = 'DROP_ZONE',
  SETTING = 'SETTING',
  PROFILE = 'PROFILE',
  MIGRATION = 'MIGRATION',
  TOOL = 'TOOL',
  PLUGIN = 'PLUGIN',
}

// Core event types
export enum CoreEventType {
  APPLICATION_STARTED = 'APPLICATION_STARTED',
  APPLICATION_SHUTDOWN = 'APPLICATION_SHUTDOWN',
  LOG_LEVEL_CHANGED = 'LOG_LEVEL_CHANGED',
}

// Mod event types
export enum ModEventType {
  LOADED = 'LOADED',
  UNLOADED = 'UNLOADED',
  DELETED = 'DELETED',
  IMPORTED = 'IMPORTED',
  REFRESHED = 'REFRESHED',
  CLASSIFICATION_TREE_CHANGED = 'CLASSIFICATION_TREE_CHANGED',
  METADATA_UPDATED = 'METADATA_UPDATED',
  CATEGORY_UPDATED = 'CATEGORY_UPDATED',
  PREVIEW_IMPORTED = 'PREVIEW_IMPORTED',
  THUMBNAIL_UPDATED = 'THUMBNAIL_UPDATED',
  PREVIEW_DELETED = 'PREVIEW_DELETED',
}

// Drop zone event types
export enum DropZoneEventType {
  CLICK = 'CLICK',
  DRAG_ENTER = 'DRAG_ENTER',
  DRAG_LEAVE = 'DRAG_LEAVE',
  FILE_DROP = 'FILE_DROP',
  MOUSE_ENTER = 'MOUSE_ENTER',
  MOUSE_LEAVE = 'MOUSE_LEAVE',
}

// TaskQueue event types
export enum TaskQueueEventType {
  ADDED = 'ADDED',
  STARTED = 'STARTED',
  PROGRESS = 'PROGRESS',
  COMPLETED = 'COMPLETED',
  FAILED = 'FAILED',
  CANCELLED = 'CANCELLED',
  REMOVED = 'REMOVED',
  AWAITING_CONFIRMATION = 'AWAITING_CONFIRMATION',
}

// Settings event types
export enum SettingsEventType {
  WINDOW_STATE_RESET = 'WINDOW_STATE_RESET',
  GLOBAL_SETTINGS_CHANGED = 'GLOBAL_SETTINGS_CHANGED',
}

// Profile event types
export enum ProfileEventType {
  CREATED = 'CREATED',
  UPDATED = 'UPDATED',
  DELETED = 'DELETED',
  DUPLICATED = 'DUPLICATED',
  SWITCHED = 'SWITCHED',
  CONFIG_UPDATED = 'CONFIG_UPDATED',
  CUSTOM_EVENT = 'CUSTOM_EVENT',
}

// Migration event types
export enum MigrationEventType {
  CLASSIFICATION_TREE_CHANGED = 'CLASSIFICATION_TREE_CHANGED',
  MODS_REFRESHED = 'MODS_REFRESHED',
  CUSTOM_EVENT = 'CUSTOM_EVENT',
}

// Tools event types
export enum ToolsEventType {
  CUSTOM_EVENT = 'CUSTOM_EVENT',
}

// Plugin event types
export enum PluginEventType {
  CUSTOM_EVENT = 'CUSTOM_EVENT',
}

// Map each module to its valid event type enum
export interface ModuleEventTypeMap {
  [Module.CORE]: CoreEventType;
  [Module.MOD]: ModEventType;
  [Module.DROP_ZONE]: DropZoneEventType;
  [Module.TASK_QUEUE]: TaskQueueEventType;
  [Module.SETTING]: SettingsEventType;
  [Module.PROFILE]: ProfileEventType;
  [Module.MIGRATION]: MigrationEventType;
  [Module.TOOL]: ToolsEventType;
  [Module.PLUGIN]: PluginEventType;
}

// Event payload type mapping
export interface EventPayloadMap {
  // Core events
  [Module.CORE]: {
    [CoreEventType.APPLICATION_STARTED]: void;
    [CoreEventType.APPLICATION_SHUTDOWN]: void;
    [CoreEventType.LOG_LEVEL_CHANGED]: { level: string };
  };

  // Mod events
  [Module.MOD]: {
    [ModEventType.LOADED]: { sha: string };
    [ModEventType.UNLOADED]: { sha: string };
    [ModEventType.DELETED]: { sha: string; mod?: ModInfo };
    [ModEventType.IMPORTED]: ModInfo;
    [ModEventType.REFRESHED]: void;
    [ModEventType.CLASSIFICATION_TREE_CHANGED]: unknown;
    [ModEventType.METADATA_UPDATED]: { sha: string; mod?: ModInfo };
    [ModEventType.CATEGORY_UPDATED]: { sha: string; category: string; mod?: ModInfo };
    [ModEventType.PREVIEW_IMPORTED]: { sha: string; imagePath?: string };
    [ModEventType.THUMBNAIL_UPDATED]: { sha: string; previewPath: string };
    [ModEventType.PREVIEW_DELETED]: { sha: string; previewPath: string };
  };

  // Drop zone events
  [Module.DROP_ZONE]: {
    [DropZoneEventType.CLICK]: { zoneId: string; position: { x: number; y: number } };
    [DropZoneEventType.DRAG_ENTER]: { zoneId: string };
    [DropZoneEventType.DRAG_LEAVE]: { zoneId: string };
    [DropZoneEventType.FILE_DROP]: { zoneId: string; files: string[] };
    [DropZoneEventType.MOUSE_ENTER]: { zoneId: string };
    [DropZoneEventType.MOUSE_LEAVE]: { zoneId: string };
  };

  // TaskQueue events
  [Module.TASK_QUEUE]: {
    [TaskQueueEventType.ADDED]: TaskInfo;
    [TaskQueueEventType.STARTED]: TaskInfo;
    [TaskQueueEventType.PROGRESS]: TaskProgress;
    [TaskQueueEventType.COMPLETED]: TaskInfo;
    [TaskQueueEventType.FAILED]: TaskInfo;
    [TaskQueueEventType.CANCELLED]: TaskInfo;
    [TaskQueueEventType.REMOVED]: TaskInfo;
    [TaskQueueEventType.AWAITING_CONFIRMATION]: TaskInfo;
  };

  // Settings events
  [Module.SETTING]: {
    [SettingsEventType.WINDOW_STATE_RESET]: void;
    [SettingsEventType.GLOBAL_SETTINGS_CHANGED]: unknown;
  };

  // Profile events
  [Module.PROFILE]: {
    [ProfileEventType.CUSTOM_EVENT]: unknown;
  };

  // Migration events
  [Module.MIGRATION]: {
    [MigrationEventType.CLASSIFICATION_TREE_CHANGED]: unknown;
    [MigrationEventType.MODS_REFRESHED]: unknown;
    [MigrationEventType.CUSTOM_EVENT]: unknown;
  };

  // Tools events
  [Module.TOOL]: {
    [ToolsEventType.CUSTOM_EVENT]: unknown;
  };

  // Plugins events
  [Module.PLUGIN]: {
    [PluginEventType.CUSTOM_EVENT]: unknown;
  };
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
  event: Event<M, T>
) => void;

/**
 * Event Bus for backend-to-frontend event streaming
 */
class EventBus {
  private handlers: Map<string, Set<EventHandler<any, any>>> = new Map();

  /**
   * Subscribe to specific module and event type
   * Handler receives typed Event with payload property
   * Returns cleanup function for use in React useEffect
   */
  subscribe<M extends Module, T extends string>(
    module: M,
    type: T,
    handler: EventHandler<M, T>
  ): () => void {
    const key = this.getEventKey(module, type);

    if (!this.handlers.has(key)) {
      this.handlers.set(key, new Set());
    }

    this.handlers.get(key)!.add(handler);

    // Return cleanup function
    return () => {
      const handlers = this.handlers.get(key);
      if (handlers) {
        handlers.delete(handler);
        if (handlers.size === 0) {
          this.handlers.delete(key);
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
          console.error(`Error in event handler for ${key}:`, error);
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
  ...CoreEventType,
  ...ModEventType,
  ...DropZoneEventType,
  ...TaskQueueEventType,
  ...SettingsEventType,
  ...ProfileEventType,
  ...MigrationEventType,
  ...ToolsEventType,
  ...PluginEventType,
} as const;

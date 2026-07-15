/**
 * Event Bus - Centralized event subscription pattern for backend-to-frontend events
 * Supports typed events with compile-time safety
 */

import type { MigrationProgress, MigrationResult } from "../types/migration.types";
import type { WorkflowInfo } from "../../modules/workflow/types/workflow.types";
import type { ModInfo } from "../types/mod.types";
import type { PackageProgress, ExportResult, ImportResult } from "../types/modPackage.types";
import type { AnalysisProgress, FullAnalysisReport } from "../types/analysis.types";
import type { ModIdMigrationScanResult, ModIdMigrationProgress, ModIdMigrationResult } from "../types/modIdMigration.types";
import type { ModFixProgress, ModFixResult } from "../types/modFix.types";
import type { OrphanScanResult, CleanupResult } from "../types/cleanup.types";
import type { ProcessInfo } from "../store/processStore";

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
}

// Core event types
export enum SystemEventType {
  APPLICATION_STARTED = "APPLICATION_STARTED",
  APPLICATION_SHUTDOWN = "APPLICATION_SHUTDOWN",
  LOG_LEVEL_CHANGED = "LOG_LEVEL_CHANGED",
  PROCESS_LIST_UPDATED = "PROCESS_LIST_UPDATED",
  PROCESS_RESUME_REQUESTED = "PROCESS_RESUME_REQUESTED",
  ONLINE_ACCOUNT_CHANGED = "ONLINE_ACCOUNT_CHANGED",
  LOGIN_WINDOW_SHOWN = "LOGIN_WINDOW_SHOWN",
}

// Mod event types
export enum ModEventType {
  LOADING = "LOADING",
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
  CACHE_CHANGED = "CACHE_CHANGED",
  MOD_LIST_UPDATED = "MOD_LIST_UPDATED",
  LOCATE_REQUESTED = "LOCATE_REQUESTED",
  PRESET_SAVED = "PRESET_SAVED",
  PRESET_DELETED = "PRESET_DELETED",
  PRESET_APPLIED = "PRESET_APPLIED",
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
  CAPTURE_BOUNDS_CHANGED = "CAPTURE_BOUNDS_CHANGED",
  MOD_PACKAGE_PROGRESS = "MOD_PACKAGE_PROGRESS",
  MOD_PACKAGE_EXPORT_COMPLETE = "MOD_PACKAGE_EXPORT_COMPLETE",
  MOD_PACKAGE_IMPORT_COMPLETE = "MOD_PACKAGE_IMPORT_COMPLETE",
  MOD_ANALYSIS_PROGRESS = "MOD_ANALYSIS_PROGRESS",
  MOD_ANALYSIS_COMPLETE = "MOD_ANALYSIS_COMPLETE",
  MOD_ID_MIGRATION_SCAN_COMPLETE = "MOD_ID_MIGRATION_SCAN_COMPLETE",
  MOD_ID_MIGRATION_PROGRESS = "MOD_ID_MIGRATION_PROGRESS",
  MOD_ID_MIGRATION_COMPLETE = "MOD_ID_MIGRATION_COMPLETE",
  MOD_FIX_PROGRESS = "MOD_FIX_PROGRESS",
  MOD_FIX_COMPLETE = "MOD_FIX_COMPLETE",
  FIX_TOOLS_CHANGED = "FIX_TOOLS_CHANGED",
  ORPHAN_SCAN_COMPLETE = "ORPHAN_SCAN_COMPLETE",
  ORPHAN_CLEAN_COMPLETE = "ORPHAN_CLEAN_COMPLETE",
}

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
}

// Event payload type mapping
export interface EventPayloadMap {
  // System events
  [Module.SYSTEM]: {
    [SystemEventType.APPLICATION_STARTED]: void;
    [SystemEventType.APPLICATION_SHUTDOWN]: void;
    [SystemEventType.LOG_LEVEL_CHANGED]: { level: string };
    [SystemEventType.PROCESS_LIST_UPDATED]: { processes?: ProcessInfo[] };
    // A crash-interrupted resumable op re-announced from its profile DB checkpoint — carries the resume
    // key + owning profile (which the TS ProcessInfo doesn't model on its own).
    [SystemEventType.PROCESS_RESUME_REQUESTED]: ProcessInfo & { resumePayload?: string; profileId?: string };
    [SystemEventType.ONLINE_ACCOUNT_CHANGED]: void;
    [SystemEventType.LOGIN_WINDOW_SHOWN]: void;
  };

  // Mod events
  [Module.MOD]: {
    [ModEventType.LOADING]: { id: string };
    [ModEventType.LOADED]: { id: string };
    [ModEventType.UNLOADED]: { id: string };
    [ModEventType.DELETED]: { id: string; mod?: ModInfo };
    [ModEventType.IMPORTED]: ModInfo;
    [ModEventType.REFRESHED]: void;
    [ModEventType.METADATA_UPDATED]: { id: string; mod?: ModInfo };
    [ModEventType.CATEGORY_UPDATED]: {
      id: string;
      category: string;
      mod?: ModInfo;
    };
    [ModEventType.PREVIEW_IMPORTED]: { id: string; imagePath?: string };
    [ModEventType.THUMBNAIL_UPDATED]: { id: string; previewPath: string };
    [ModEventType.PREVIEW_DELETED]: { id: string; previewPath: string };
    [ModEventType.CACHE_CHANGED]: {
      id: string;
      wasLoaded?: boolean;
      nowLoaded?: boolean;
      changeType: 'deleted' | 'renamed';
    };
    [ModEventType.MOD_LIST_UPDATED]: void;
    [ModEventType.LOCATE_REQUESTED]: { modIds: string[]; categoryId?: string };
    [ModEventType.PRESET_SAVED]: { id: string; name: string };
    [ModEventType.PRESET_DELETED]: { id: string };
    [ModEventType.PRESET_APPLIED]: { id: string; name: string };
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
  };

  // Workflow events
  [Module.WORKFLOW]: {
    [WorkflowEventType.CREATED]: WorkflowInfo;
    [WorkflowEventType.STATUS_CHANGED]: WorkflowInfo;
    [WorkflowEventType.COMPLETED]: WorkflowInfo;
    [WorkflowEventType.FAILED]: WorkflowInfo;
    [WorkflowEventType.CANCELLED]: WorkflowInfo;
    [WorkflowEventType.PROGRESS]: { workflowId: string; progress: number; step: string };
    [WorkflowEventType.DELETED]: string; // the deleted workflow's id
  };

  // Settings events
  [Module.SETTING]: {
    [SettingsEventType.WINDOW_STATE_RESET]: void;
    [SettingsEventType.GLOBAL_SETTINGS_CHANGED]: {
      theme: string;
      annotationLevel: string;
      logLevel: string;
      language: string;
      lastUpdated: string;
    };
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
    [ToolsEventType.CAPTURE_BOUNDS_CHANGED]: { x: number; y: number; width: number; height: number };
    [ToolsEventType.MOD_PACKAGE_PROGRESS]: PackageProgress;
    [ToolsEventType.MOD_PACKAGE_EXPORT_COMPLETE]: ExportResult;
    [ToolsEventType.MOD_PACKAGE_IMPORT_COMPLETE]: ImportResult;
    [ToolsEventType.MOD_ANALYSIS_PROGRESS]: AnalysisProgress;
    [ToolsEventType.MOD_ANALYSIS_COMPLETE]: FullAnalysisReport;
    [ToolsEventType.MOD_ID_MIGRATION_SCAN_COMPLETE]: ModIdMigrationScanResult;
    [ToolsEventType.MOD_ID_MIGRATION_PROGRESS]: ModIdMigrationProgress;
    [ToolsEventType.MOD_ID_MIGRATION_COMPLETE]: ModIdMigrationResult;
    [ToolsEventType.MOD_FIX_PROGRESS]: ModFixProgress;
    [ToolsEventType.MOD_FIX_COMPLETE]: ModFixResult;
    [ToolsEventType.FIX_TOOLS_CHANGED]: unknown;
    [ToolsEventType.ORPHAN_SCAN_COMPLETE]: { results: OrphanScanResult[]; error?: string };
    [ToolsEventType.ORPHAN_CLEAN_COMPLETE]: CleanupResult;
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
  event: Event<M, T>,
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
    handler: EventHandler<M, T>,
  ): () => void {
    const key = this.getEventKey(module, type);

    let handlers = this.handlers.get(key);

    if (!handlers) {
      handlers = new Set();
      this.handlers.set(key, handlers);
    }

    handlers.add(handler);

    // Return cleanup function
    return () => {
      const handlers = this.handlers.get(key);
      if (handlers) {
        handlers.delete(handler);

        // If no more handlers, remove the key
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
  ...SystemEventType,
  ...ModEventType,
  ...DropZoneEventType,
  ...WorkflowEventType,
  ...SettingsEventType,
  ...ProfileEventType,
  ...MigrationEventType,
  ...ToolsEventType,
} as const;

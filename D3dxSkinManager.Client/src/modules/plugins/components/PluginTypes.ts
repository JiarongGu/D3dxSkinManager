/**
 * Frontend plugin system types
 *
 * Plugins can extend the UI with custom components, hooks, and functionality.
 */

import { ReactNode } from 'react';
import { ModInfo } from '../../../shared/types/mod.types';
import type { ModService } from '../../mods/services/modService';

/**
 * Base plugin interface
 */
export interface Plugin {
  /** Unique plugin identifier (e.g., "com.example.myplugin") */
  id: string;

  /** Human-readable name */
  name: string;

  /** Plugin version */
  version: string;

  /** Plugin description */
  description: string;

  /** Plugin author */
  author: string;

  /** Initialize the plugin */
  initialize(context: PluginContext): void | Promise<void>;

  /** Cleanup when plugin is unloaded */
  cleanup(): void | Promise<void>;
}

/**
 * Plugin context providing access to app services
 */
export interface PluginContext {
  /** Access to mod service for backend communication */
  modService: ModService;

  /** Register a custom event handler */
  registerEventHandler(eventType: PluginEventType, handler: PluginEventHandler): string;

  /** Unregister an event handler */
  unregisterEventHandler(registrationId: string): void;

  /** Emit a custom event */
  emitEvent(eventName: string, data?: unknown): void;
}

/**
 * Plugin event types
 */
export enum PluginEventType {
  ModLoaded = 'MOD_LOADED',
  ModUnloaded = 'MOD_UNLOADED',
  ModImported = 'MOD_IMPORTED',
  ModDeleted = 'MOD_DELETED',
  ModsRefreshed = 'MODS_REFRESHED',
  CustomEvent = 'CUSTOM_EVENT'
}

/**
 * Plugin event arguments
 */
export interface PluginEventArgs {
  eventType: PluginEventType;
  eventName?: string;  // For CustomEvent type
  data?: unknown;
  timestamp: Date;
}

/**
 * Plugin event handler function type
 */
export type PluginEventHandler = (args: PluginEventArgs) => void | Promise<void>;

/**
 * UI plugin interface - extends base plugin with UI components
 */
export interface UIPlugin extends Plugin {
  /** Optional: Render custom tab content */
  renderTab?(): ReactNode;

  /** Optional: Tab label (if renderTab is provided) */
  tabLabel?: string;

  /** Optional: Tab icon (Ant Design icon name) */
  tabIcon?: string;

  /** Optional: Render additional sidebar items */
  renderSidebarItems?(): ReactNode;

  /** Optional: Render custom modal/dialog */
  renderModal?(): ReactNode;

  /** Optional: Render custom menu items */
  renderMenuItems?(): ReactNode;
}

/**
 * Action plugin interface - adds custom mod actions
 */
export interface ActionPlugin extends Plugin {
  /** Get custom actions for a mod */
  getModActions(mod: ModInfo): ModAction[];
}

/**
 * Custom mod action
 */
export interface ModAction {
  key: string;
  label: string;
  icon?: string;
  onClick: (mod: ModInfo) => void | Promise<void>;
}

/**
 * Plugin metadata for the registry
 */
export interface PluginMetadata {
  id: string;
  name: string;
  version: string;
  description: string;
  author: string;
  enabled: boolean;
  instance?: Plugin;
}

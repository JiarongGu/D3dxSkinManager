/**
 * Frontend plugin system types
 *
 * NOTE: Frontend plugins are UI-only extensions. Backend plugins (C# .dll) are separate
 * and communicate via IPC messages. Frontend plugins subscribe to EventBus events.
 */

import { ReactNode } from 'react';
import { ModInfo } from '../../../shared/types/mod.types';
import type { ModService } from '../../mods/services/modService';
import type { Event, Module } from '../../../shared/services/eventBus';

export interface Plugin {
  id: string;
  name: string;
  version: string;
  description: string;
  author: string;

  Init(context: PluginContext): void | Promise<void>;
  cleanup(): void | Promise<void>;
}

/**
 * Plugin context providing access to services and EventBus
 */
export interface PluginContext {
  modService: ModService;

  /** Subscribe to EventBus events using module + type pattern */
  subscribeEvent<M extends Module, T extends string>(
    module: M,
    type: T,
    handler: (event: Event<M, T>) => void
  ): () => void;
}

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

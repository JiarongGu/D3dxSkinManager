/**
 * Frontend plugin registry
 *
 * Manages plugin loading, registration, and lifecycle.
 */

import {
  Plugin,
  PluginContext,
  PluginMetadata,
  UIPlugin,
  ActionPlugin,
  PluginEventType,
  PluginEventArgs,
  PluginEventHandler
} from './PluginTypes';

class PluginRegistry {
  private plugins: Map<string, PluginMetadata> = new Map();
  private eventHandlers: Map<string, PluginEventHandler> = new Map();
  private eventHandlerCounter = 0;

  /**
   * Register a plugin
   */
  register(plugin: Plugin, context: PluginContext): void {
    if (this.plugins.has(plugin.id)) {
      console.warn(`[PluginRegistry] Plugin ${plugin.id} is already registered`);
      return;
    }

    const metadata: PluginMetadata = {
      id: plugin.id,
      name: plugin.name,
      version: plugin.version,
      description: plugin.description,
      author: plugin.author,
      enabled: true,
      instance: plugin
    };

    this.plugins.set(plugin.id, metadata);

    // Initialize plugin
    try {
      const result = plugin.initialize(context);
      if (result instanceof Promise) {
        result.catch(err => {
          console.error(`[PluginRegistry] Error initializing plugin ${plugin.name}:`, err);
        });
      }
      console.log(`[PluginRegistry] Registered plugin: ${plugin.name} v${plugin.version}`);
    } catch (err) {
      console.error(`[PluginRegistry] Error initializing plugin ${plugin.name}:`, err);
    }
  }

  /**
   * Unregister a plugin
   */
  unregister(pluginId: string): void {
    const metadata = this.plugins.get(pluginId);
    if (!metadata || !metadata.instance) return;

    try {
      const result = metadata.instance.cleanup();
      if (result instanceof Promise) {
        result.catch(err => {
          console.error(`[PluginRegistry] Error cleaning up plugin ${metadata.name}:`, err);
        });
      }
    } catch (err) {
      console.error(`[PluginRegistry] Error cleaning up plugin ${metadata.name}:`, err);
    }

    this.plugins.delete(pluginId);
    console.log(`[PluginRegistry] Unregistered plugin: ${metadata.name}`);
  }

  /**
   * Get a plugin by ID
   */
  getPlugin(pluginId: string): Plugin | undefined {
    return this.plugins.get(pluginId)?.instance;
  }

  /**
   * Get all registered plugins
   */
  getAllPlugins(): PluginMetadata[] {
    return Array.from(this.plugins.values());
  }

  /**
   * Get all UI plugins
   */
  getUIPlugins(): UIPlugin[] {
    return Array.from(this.plugins.values())
      .filter(m => m.instance && 'renderTab' in m.instance)
      .map(m => m.instance as UIPlugin);
  }

  /**
   * Get all action plugins
   */
  getActionPlugins(): ActionPlugin[] {
    return Array.from(this.plugins.values())
      .filter(m => m.instance && 'getModActions' in m.instance)
      .map(m => m.instance as ActionPlugin);
  }

  /**
   * Register an event handler
   */
  registerEventHandler(eventType: PluginEventType, handler: PluginEventHandler): string {
    const registrationId = `${eventType}_${++this.eventHandlerCounter}_${Date.now()}`;
    this.eventHandlers.set(registrationId, handler);
    return registrationId;
  }

  /**
   * Unregister an event handler
   */
  unregisterEventHandler(registrationId: string): void {
    this.eventHandlers.delete(registrationId);
  }

  /**
   * Emit an event to all registered handlers
   */
  async emitEvent(args: PluginEventArgs): Promise<void> {
    const handlersToInvoke = Array.from(this.eventHandlers.entries())
      .filter(([id]) => id.startsWith(`${args.eventType}_`))
      .map(([_, handler]) => handler);

    const promises = handlersToInvoke.map(async handler => {
      try {
        await handler(args);
      } catch (err) {
        console.error('[PluginRegistry] Error in event handler:', err);
      }
    });

    await Promise.all(promises);
  }

  /**
   * Enable a plugin
   */
  enablePlugin(pluginId: string): void {
    const metadata = this.plugins.get(pluginId);
    if (metadata) {
      metadata.enabled = true;
    }
  }

  /**
   * Disable a plugin
   */
  disablePlugin(pluginId: string): void {
    const metadata = this.plugins.get(pluginId);
    if (metadata) {
      metadata.enabled = false;
    }
  }

  /**
   * Get plugin count
   */
  getPluginCount(): number {
    return this.plugins.size;
  }
}

// Singleton instance
export const pluginRegistry = new PluginRegistry();

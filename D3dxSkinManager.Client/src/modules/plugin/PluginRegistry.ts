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
  ActionPlugin
} from './components/PluginTypes';

class PluginRegistry {
  private plugins: Map<string, PluginMetadata> = new Map();

  /**
   * Register a plugin
   */
  register(plugin: Plugin, context: PluginContext): void {
    if (this.plugins.has(plugin.id)) {
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
      const result = plugin.Init(context);
      if (result instanceof Promise) {
        result.catch(err => {
                  });
      }
          } catch (err: unknown) {
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
                  });
      }
    } catch (err: unknown) {
          }

    this.plugins.delete(pluginId);
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

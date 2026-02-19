/**
 * React hook for plugin system integration
 */

import { useEffect, useState } from 'react';
import { pluginRegistry } from './PluginRegistry';
import { PluginContext, PluginEventType, PluginEventArgs } from './PluginTypes';
import { modService } from '../../mods/services/modService';

/**
 * Hook to initialize and access the plugin system
 */
export const usePluginSystem = () => {
  const [pluginCount, setPluginCount] = useState(0);
  const [initialized, setInitialized] = useState(false);

  useEffect(() => {
    // Create plugin context
    const context: PluginContext = {
      modService: modService,
      registerEventHandler: (eventType, handler) =>
        pluginRegistry.registerEventHandler(eventType, handler),
      unregisterEventHandler: (registrationId) =>
        pluginRegistry.unregisterEventHandler(registrationId),
      emitEvent: (eventName, data) =>
        pluginRegistry.emitEvent({
          eventType: PluginEventType.CustomEvent,
          eventName,
          data,
          timestamp: new Date()
        })
    };

    // Load plugins (in a real implementation, this would load from a plugins directory)
    // For now, plugins would be imported and registered manually
    // Example:
    // import { MyPlugin } from './plugins/MyPlugin';
    // pluginRegistry.register(new MyPlugin(), context);

    setPluginCount(pluginRegistry.getPluginCount());
    setInitialized(true);

    console.log('[Plugin System] Initialized');

    // Cleanup
    return () => {
      const plugins = pluginRegistry.getAllPlugins();
      plugins.forEach(p => {
        if (p.instance) {
          pluginRegistry.unregister(p.id);
        }
      });
    };
  }, []);

  return {
    registry: pluginRegistry,
    pluginCount,
    initialized,
    emitModLoadedEvent: (sha: string) => {
      pluginRegistry.emitEvent({
        eventType: PluginEventType.ModLoaded,
        data: { sha },
        timestamp: new Date()
      });
    },
    emitModUnloadedEvent: (sha: string) => {
      pluginRegistry.emitEvent({
        eventType: PluginEventType.ModUnloaded,
        data: { sha },
        timestamp: new Date()
      });
    },
    emitModImportedEvent: (mod: any) => {
      pluginRegistry.emitEvent({
        eventType: PluginEventType.ModImported,
        data: mod,
        timestamp: new Date()
      });
    },
    emitModDeletedEvent: (sha: string) => {
      pluginRegistry.emitEvent({
        eventType: PluginEventType.ModDeleted,
        data: { sha },
        timestamp: new Date()
      });
    },
    emitModsRefreshedEvent: () => {
      pluginRegistry.emitEvent({
        eventType: PluginEventType.ModsRefreshed,
        timestamp: new Date()
      });
    }
  };
};

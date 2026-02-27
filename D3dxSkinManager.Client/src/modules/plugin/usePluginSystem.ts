/**
 * React hook for plugin system integration
 */

import { useEffect, useState } from 'react';
import { pluginRegistry } from './PluginRegistry';
import { PluginContext } from './components/PluginTypes';
import { modService } from '../mod/services/modService';
import { eventBus } from '../../shared/services/eventBus';

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
      subscribeEvent: (module, type, handler) => {
        return eventBus.subscribe(module, type, handler);
      }
    };

    // Load plugins (in a real implementation, this would load from a plugins directory)
    // For now, plugins would be imported and registered manually
    // Example:
    // import { MyPlugin } from './plugins/MyPlugin';
    // pluginRegistry.register(new MyPlugin(), context);

    setPluginCount(pluginRegistry.getPluginCount());
    setInitialized(true);

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
    initialized
  };
};

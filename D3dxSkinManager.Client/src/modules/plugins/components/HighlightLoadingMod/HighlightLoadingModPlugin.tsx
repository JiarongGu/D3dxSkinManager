/**
 * Frontend plugin for highlighting loaded mods in the UI
 * Works with backend HighlightLoadingModPlugin
 *
 * Features:
 * - Receives loaded/unloaded events from backend
 * - Provides visual highlighting for loaded mods
 * - Exposes loaded mods map to UI components
 */

import { Plugin, PluginContext, PluginEventType } from '../PluginTypes';
import { photinoService } from '../../../../shared/services/photinoService';

export class HighlightLoadingModPlugin implements Plugin {
  id = 'com.d3dxskinmanager.highlightloadingmod.frontend';
  name = 'Highlight Loading Mod (Frontend)';
  version = '1.0.0';
  description = 'Highlights currently loaded mods in the UI';
  author = 'D3dxSkinManager Team';

  private context?: PluginContext;
  private loadedModsMap: Map<string, string> = new Map(); // category -> sha

  async initialize(context: PluginContext): Promise<void> {
    this.context = context;

    // Register event handlers
    context.registerEventHandler(PluginEventType.ModLoaded, this.onModLoaded);
    context.registerEventHandler(PluginEventType.ModUnloaded, this.onModUnloaded);
    context.registerEventHandler(PluginEventType.CustomEvent, this.onCustomEvent);

    // Load initial state from backend
    await this.refreshLoadedMods();

    console.log(`[${this.name}] Initialized`);
  }

  async cleanup(): Promise<void> {
    this.loadedModsMap.clear();
    console.log(`[${this.name}] Cleaned up`);
  }

  // ============= Event Handlers =============

  private onModLoaded = async (args: any) => {
    const { sha } = args.data || {};
    console.log(`[${this.name}] Mod loaded event:`, sha);
    await this.refreshLoadedMods();
  };

  private onModUnloaded = async (args: any) => {
    const { sha } = args.data || {};
    console.log(`[${this.name}] Mod unloaded event:`, sha);
    await this.refreshLoadedMods();
  };

  private onCustomEvent = async (args: any) => {
    if (args.eventName === 'HIGHLIGHT_MOD_LOADED') {
      const { sha, category } = args.data || {};
      if (sha && category) {
        this.loadedModsMap.set(category, sha);
        console.log(`[${this.name}] Highlighted: ${category} -> ${sha}`);
      }
    } else if (args.eventName === 'HIGHLIGHT_MOD_UNLOADED') {
      const { category } = args.data || {};
      if (category) {
        this.loadedModsMap.delete(category);
        console.log(`[${this.name}] Unhighlighted: ${category}`);
      }
    }
  };

  // ============= Public API =============

  /**
   * Get the loaded mod SHA for a specific object
   */
  public getLoadedModForObject(category: string): string | undefined {
    return this.loadedModsMap.get(category);
  }

  /**
   * Check if a mod is currently loaded
   */
  public isModLoaded(sha: string): boolean {
    return Array.from(this.loadedModsMap.values()).includes(sha);
  }

  /**
   * Get all loaded mods map
   */
  public getLoadedModsMap(): Map<string, string> {
    return new Map(this.loadedModsMap);
  }

  /**
   * Check if an object has a loaded mod
   */
  public hasLoadedMod(category: string): boolean {
    return this.loadedModsMap.has(category);
  }

  // ============= Helper Methods =============

  /**
   * Refresh loaded mods from backend
   */
  private async refreshLoadedMods(profileId?: string): Promise<void> {
    try {
      const response = await photinoService.sendMessage<{loadedMods: Record<string, string>}>({
        module: 'PLUGINS',
        type: 'GET_LOADED_MODS_MAP',
        profileId: profileId
      });

      this.loadedModsMap.clear();
      Object.entries(response.loadedMods || {}).forEach(([category, sha]) => {
        this.loadedModsMap.set(category, sha);
      });

      console.log(`[${this.name}] Refreshed loaded mods:`, this.loadedModsMap.size);
    } catch (err) {
      console.error(`[${this.name}] Error refreshing loaded mods:`, err);
    }
  }
}

/**
 * React hook to access highlight plugin functionality
 */
export const useHighlightLoadingMod = () => {
  // This would be implemented in a React context provider
  // For now, plugins need to be accessed via the registry
  return {
    getLoadedModForObject: (category: string) => {
      // Access plugin from registry
      return undefined;
    },
    isModLoaded: (sha: string) => false,
    hasLoadedMod: (category: string) => false
  };
};

import { BaseModuleService } from '../baseModuleService';

/** A loaded plugin as reported by the backend registry (PLUGIN/GET_ALL). */
export interface PluginInfo {
  id: string;
  name: string;
  version: string;
  description: string;
  author: string;
  isEnabled: boolean;
  /** Typed capabilities the plugin exposes (e.g. "ImageReview", "MessageHandler"). */
  capabilities: string[];
}

/** Update status for an installed official pack (PLUGIN/CHECK_UPDATES). */
export interface PluginUpdateInfo {
  pluginId: string;
  packId: string;
  installedVersion: string;
  availableVersion: string;
  updateAvailable: boolean;
}

/**
 * Plugin management IPC (PLUGIN module — profile-scoped: plugins load from {profile}/plugins).
 * Backend: PluginFacade.
 */
export class PluginService extends BaseModuleService {
  constructor() {
    super('PLUGIN');
  }

  /** All plugins loaded for the profile. */
  async getAll(profileId: string): Promise<PluginInfo[]> {
    return this.sendArrayMessage<PluginInfo>('GET_ALL', profileId);
  }

  /** The profile's plugins directory (install target for plugin packs). */
  async getDirectory(profileId: string): Promise<{ path: string }> {
    return this.sendMessage<{ path: string }>('GET_DIRECTORY', profileId);
  }

  /** Enable/disable a loaded plugin (instant, persisted per profile). */
  async setEnabled(profileId: string, pluginId: string, enabled: boolean): Promise<void> {
    await this.sendMessage(enabled ? 'ENABLE' : 'DISABLE', profileId, { pluginId });
  }

  /** Fire-and-forget official pack install (download → extract → live load; see Activity). */
  async downloadPack(profileId: string, packId: string): Promise<void> {
    await this.sendMessage<{ started: boolean }>('DOWNLOAD_PACK', profileId, { packId });
  }

  /**
   * Update status for each installed official pack (installed vs latest-release version).
   * Backend: PluginFacade.CHECK_UPDATES → PluginInstallService.CheckUpdatesAsync (network-tolerant:
   * returns [] when offline / no release).
   */
  async checkUpdates(profileId: string): Promise<PluginUpdateInfo[]> {
    return this.sendArrayMessage<PluginUpdateInfo>('CHECK_UPDATES', profileId);
  }
}

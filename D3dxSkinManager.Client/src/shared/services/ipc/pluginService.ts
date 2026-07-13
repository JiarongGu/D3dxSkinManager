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

/** An available official pack from the plugin repo manifest (PLUGIN/GET_AVAILABLE_PACKS) — the app has
 *  no hard-coded plugin list; the catalog is pulled from the plugin repo's latest release. */
export interface PluginPackInfo {
  id: string;
  name: string;
  description: string;
  version: string;
  asset: string;
  sdkContractVersion: string;
  compatible: boolean;
  installed: boolean;
}

/** A pack installed on disk but that FAILED to load (PLUGIN/GET_LOAD_FAILURES) — usually an SDK/contract
 *  mismatch after an app update. Since it never registered it isn't in GET_ALL; this surfaces it so the UI
 *  can flag "requires update" and offer a download when a compatible build exists. */
export interface PluginLoadFailure {
  packId: string;
  dllName: string;
  reason: string;
  /** Display name from the catalog when known; else undefined (fall back to packId). */
  name?: string;
  /** A compatible newer build exists in the catalog → the user can download it to fix this. */
  updateAvailable: boolean;
  /** The version the catalog offers (set only when updateAvailable). */
  availableVersion?: string;
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

  /**
   * Available official packs, pulled from the PLUGIN REPO's latest release manifest (no hard-coded list
   * in the app). Each carries compatibility + installed flags. Network-tolerant: [] when offline.
   * Backend: PluginFacade.GET_AVAILABLE_PACKS → PluginInstallService.GetAvailablePacksAsync.
   */
  async getAvailablePacks(profileId: string): Promise<PluginPackInfo[]> {
    return this.sendArrayMessage<PluginPackInfo>('GET_AVAILABLE_PACKS', profileId);
  }

  /**
   * Packs installed on disk that FAILED to load (contract mismatch after an app update, …), each enriched
   * with whether a compatible build can fix it. Empty when everything loaded. Network-tolerant.
   * Backend: PluginFacade.GET_LOAD_FAILURES → PluginInstallService.GetLoadFailuresAsync.
   */
  async getLoadFailures(profileId: string): Promise<PluginLoadFailure[]> {
    return this.sendArrayMessage<PluginLoadFailure>('GET_LOAD_FAILURES', profileId);
  }

  /**
   * Pack ids whose update is STAGED and awaiting a restart to apply (mirrors the app-update "pending"
   * state). Empty when nothing is staged. Backend: PluginFacade.GET_PENDING_UPDATES.
   */
  async getPendingUpdates(profileId: string): Promise<string[]> {
    return this.sendArrayMessage<string>('GET_PENDING_UPDATES', profileId);
  }
}

/**
 * Settings Service - Handles global settings storage via backend
 */

import { bridgeService } from '../bridgeService';

export interface GlobalSettings {
  theme: 'light' | 'dark' | 'auto';
  annotationLevel: string;
  logLevel: string;
  language: string;
  autoUpdateCheck: boolean;
  /** Content veil: blur previews the sensitivity heuristic flags; hover/detail/fullscreen reveal. Opt-in. */
  contentVeilEnabled: boolean;
  /** How many mod imports (extract+recompress, CPU-bound) run in parallel — the import lane (1–8, default 5). */
  maxParallelImports: number;
  /** How many remote downloads (network-bound) run in parallel — the download lane, separate from imports (1–8, default 4). */
  maxParallelDownloads: number;
  lastUpdated: string;
}

export interface SettingsUpdateResult {
  success: boolean;
  message: string;
  settings?: GlobalSettings;
}

export class SettingsService {
  /**
   * Get global settings from backend
   * No caching here - the store (useSettingsStore) handles caching.
   * This ensures we always get fresh data when called.
   */
  async getGlobalSettings(): Promise<GlobalSettings> {
    return await bridgeService.sendMessage<GlobalSettings>({
      module: 'SETTING',
      type: 'GET_GLOBAL',
      payload: {}
    });
  }

  /**
   * Update a single global setting
   */
  async updateGlobalSetting(key: string, value: string): Promise<SettingsUpdateResult> {
    return await bridgeService.sendMessage<SettingsUpdateResult>({
      module: 'SETTING',
      type: 'UPDATE_FIELD',
      payload: { key, value }
    });
  }

  /**
   * Reset window state (size and position) to defaults
   * Window will be centered on next restart
   */
  async resetWindowState(): Promise<SettingsUpdateResult> {
    return await bridgeService.sendMessage<SettingsUpdateResult>({
      module: 'SETTING',
      type: 'RESET_WINDOW_STATE',
      payload: {}
    });
  }

}

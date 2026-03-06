/**
 * Settings Service - Handles global settings storage via backend
 */

import { bridgeService } from '../bridgeService';

export interface GlobalSettings {
  theme: 'light' | 'dark' | 'auto';
  annotationLevel: string;
  logLevel: string;
  language: string;
  lastUpdated: string;
}

export interface SettingsUpdateResult {
  success: boolean;
  message: string;
  settings?: GlobalSettings;
}

export class SettingsService {
  // In-memory cache for settings to prevent duplicate calls during startup
  private settingsCache: GlobalSettings | null = null;
  private settingsCachePromise: Promise<GlobalSettings> | null = null;

  /**
   * Get global settings from backend
   *
   * OPTIMIZATION: Caches the first request and deduplicates concurrent calls.
   * This prevents 8+ simultaneous GET_GLOBAL calls during app startup when
   * multiple components (ThemeContext, logger, i18n, etc.) all try to load settings.
   */
  async getGlobalSettings(): Promise<GlobalSettings> {
    // Return cached value if available
    if (this.settingsCache) {
      return this.settingsCache;
    }

    // If a request is already in flight, return the same promise
    // This deduplicates concurrent requests
    if (this.settingsCachePromise) {
      return this.settingsCachePromise;
    }

    // Make the request and cache the promise
    this.settingsCachePromise = bridgeService.sendMessage<GlobalSettings>({
      module: 'SETTING',
      type: 'GET_GLOBAL',
      payload: {}
    }).then(settings => {
      this.settingsCache = settings;
      this.settingsCachePromise = null;
      return settings;
    }).catch(error => {
      // Clear the promise on error so we can retry
      this.settingsCachePromise = null;
      throw error;
    });

    return this.settingsCachePromise;
  }

  /**
   * Clear the settings cache (call this when settings are updated)
   */
  private clearCache(): void {
    this.settingsCache = null;
    this.settingsCachePromise = null;
  }

  /**
   * Update multiple global settings at once
   */
  async updateGlobalSettings(settings: Partial<GlobalSettings>): Promise<SettingsUpdateResult> {
    this.clearCache(); // Clear cache when settings change
    return await bridgeService.sendMessage<SettingsUpdateResult>({
      module: 'SETTING',
      type: 'UPDATE_GLOBAL',
      payload: settings
    });
  }

  /**
   * Update a single global setting
   */
  async updateGlobalSetting(key: string, value: string): Promise<SettingsUpdateResult> {
    this.clearCache(); // Clear cache when settings change
    return await bridgeService.sendMessage<SettingsUpdateResult>({
      module: 'SETTING',
      type: 'UPDATE_FIELD',
      payload: { key, value }
    });
  }

  /**
   * Reset global settings to defaults
   */
  async resetGlobalSettings(): Promise<SettingsUpdateResult> {
    this.clearCache(); // Clear cache when settings are reset
    return await bridgeService.sendMessage<SettingsUpdateResult>({
      module: 'SETTING',
      type: 'RESET_GLOBAL',
      payload: {}
    });
  }

  /**
   * Reset window state (size and position) to defaults
   * Window will be centered on next restart
   */
  async resetWindowState(): Promise<SettingsUpdateResult> {
    // Window state is part of global settings, so clear cache
    this.clearCache();
    return await bridgeService.sendMessage<SettingsUpdateResult>({
      module: 'SETTING',
      type: 'RESET_WINDOW_STATE',
      payload: {}
    });
  }

}

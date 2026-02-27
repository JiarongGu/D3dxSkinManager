/**
 * Settings Service - Handles global settings storage via backend
 */

import { bridgeService } from '../../../shared/services/bridgeService';

export interface ModTabSettings {
  panelSize: string; // Format: "categoryWidth modListWidth" (e.g., "25 40")
}

export interface TabSettings {
  mod: ModTabSettings;
}

export interface GlobalSettings {
  theme: 'light' | 'dark' | 'auto';
  annotationLevel: string;
  logLevel: string;
  language: string;
  lastUpdated: string;
  tabs: TabSettings;
}

export interface SettingsUpdateResult {
  success: boolean;
  message: string;
  settings?: GlobalSettings;
}

class SettingsService {
  /**
   * Get global settings from backend
   */
  async getGlobalSettings(): Promise<GlobalSettings> {
    return await bridgeService.sendMessage<GlobalSettings>({
      module: 'SETTING',
      type: 'GET_GLOBAL',
      payload: {}
    });
  }

  /**
   * Update multiple global settings at once
   */
  async updateGlobalSettings(settings: Partial<GlobalSettings>): Promise<SettingsUpdateResult> {
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
    return await bridgeService.sendMessage<SettingsUpdateResult>({
      module: 'SETTING',
      type: 'RESET_WINDOW_STATE',
      payload: {}
    });
  }

  /**
   * Update mod tab panel sizes
   * @param panelSize - Format: "categoryWidth modListWidth" (e.g., "25 40")
   */
  async updateModPanelSize(panelSize: string): Promise<SettingsUpdateResult> {
    return await bridgeService.sendMessage<SettingsUpdateResult>({
      module: 'SETTING',
      type: 'UPDATE_MOD_PANEL_SIZE',
      payload: { panelSize }
    });
  }
}

export const settingsService = new SettingsService();

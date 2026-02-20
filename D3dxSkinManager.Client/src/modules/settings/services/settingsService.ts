/**
 * Settings Service - Handles global settings storage via backend
 */

import { photinoService } from '../../../shared/services/photinoService';

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

class SettingsService {
  /**
   * Get global settings from backend
   */
  async getGlobalSettings(): Promise<GlobalSettings> {
    return await photinoService.sendMessage<GlobalSettings>({
      module: 'SETTINGS',
      type: 'GET_GLOBAL',
      payload: {}
    });
  }

  /**
   * Update multiple global settings at once
   */
  async updateGlobalSettings(settings: Partial<GlobalSettings>): Promise<SettingsUpdateResult> {
    return await photinoService.sendMessage<SettingsUpdateResult>({
      module: 'SETTINGS',
      type: 'UPDATE_GLOBAL',
      payload: settings
    });
  }

  /**
   * Update a single global setting
   */
  async updateGlobalSetting(key: string, value: string): Promise<SettingsUpdateResult> {
    return await photinoService.sendMessage<SettingsUpdateResult>({
      module: 'SETTINGS',
      type: 'UPDATE_FIELD',
      payload: { key, value }
    });
  }

  /**
   * Reset global settings to defaults
   */
  async resetGlobalSettings(): Promise<SettingsUpdateResult> {
    return await photinoService.sendMessage<SettingsUpdateResult>({
      module: 'SETTINGS',
      type: 'RESET_GLOBAL',
      payload: {}
    });
  }
}

export const settingsService = new SettingsService();

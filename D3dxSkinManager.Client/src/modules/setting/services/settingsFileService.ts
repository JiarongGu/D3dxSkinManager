/**
 * Settings File Service - Manages generic JSON settings files via backend
 * Files are stored in data/settings/ directory on backend
 */

import { BaseModuleService } from '../../../shared/services/baseModuleService';

export interface SettingsFileResponse {
  success: boolean;
  message?: string;
  content?: string;
}

export interface SettingsFileExistsResponse {
  exists: boolean;
}

export interface SettingsFileListResponse {
  files: string[];
}

class SettingsFileService extends BaseModuleService {
  constructor() {
    super('SETTING');
  }

  /**
   * Get a settings file by name (without .json extension)
   * Returns the parsed JSON object, or undefined if file doesn't exist
   */
  async getSettingsFile<T extends Record<string, unknown> = Record<string, unknown>>(filename: string): Promise<T | undefined> {
    try {
      const response = await this.sendMessage<SettingsFileResponse>('GET_FILE', undefined, { filename });

      if (!response.success || !response.content) {
        return undefined;
      }

      return JSON.parse(response.content) as T;
    } catch (error) {
      console.error(`[SettingsFileService] Failed to get settings file '${filename}':`, error);
      return undefined;
    }
  }

  /**
   * Save a settings file by name (without .json extension)
   * Accepts any JSON-serializable object
   */
  async saveSettingsFile(filename: string, data: Record<string, unknown>): Promise<boolean> {
    try {
      const jsonContent = JSON.stringify(data, null, 2);

      await this.sendMessage<SettingsFileResponse>('SAVE_FILE', undefined, {
        filename,
        content: jsonContent
      });

      return true;
    } catch (error) {
      console.error(`[SettingsFileService] Failed to save settings file '${filename}':`, error);
      return false;
    }
  }

  /**
   * Delete a settings file by name (without .json extension)
   */
  async deleteSettingsFile(filename: string): Promise<boolean> {
    try {
      await this.sendMessage<SettingsFileResponse>('DELETE_FILE', undefined, { filename });
      return true;
    } catch (error) {
      console.error(`[SettingsFileService] Failed to delete settings file '${filename}':`, error);
      return false;
    }
  }

  /**
   * Check if a settings file exists
   */
  async settingsFileExists(filename: string): Promise<boolean> {
    try {
      const response = await this.sendMessage<SettingsFileExistsResponse>('FILE_EXISTS', undefined, { filename });
      return response.exists;
    } catch (error) {
      console.error(`[SettingsFileService] Failed to check if settings file '${filename}' exists:`, error);
      return false;
    }
  }

  /**
   * List all settings files (returns filenames without .json extension)
   */
  async listSettingsFiles(): Promise<string[]> {
    try {
      const response = await this.sendMessage<SettingsFileListResponse>('LIST_FILES', undefined, {});
      return response.files || [];
    } catch (error) {
      console.error('[SettingsFileService] Failed to list settings files:', error);
      return [];
    }
  }
}

export const settingsFileService = new SettingsFileService();
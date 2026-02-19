/**
 * Settings File Service - Manages generic JSON settings files via backend
 * Files are stored in data/settings/ directory on backend
 */

import { photinoService } from '../../../shared/services/photinoService';

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

class SettingsFileService {
  /**
   * Get a settings file by name (without .json extension)
   * Returns the parsed JSON object, or null if file doesn't exist
   */
  async getSettingsFile<T = any>(filename: string): Promise<T | null> {
    try {
      const response = await photinoService.sendMessage<SettingsFileResponse>({
        module: 'SETTINGS',
        type: 'GET_FILE',
        payload: { filename }
      });

      if (!response.success || !response.content) {
        return null;
      }

      return JSON.parse(response.content) as T;
    } catch (error) {
      console.error(`[SettingsFileService] Failed to get settings file '${filename}':`, error);
      return null;
    }
  }

  /**
   * Save a settings file by name (without .json extension)
   * Accepts any JSON-serializable object
   */
  async saveSettingsFile(filename: string, data: any): Promise<boolean> {
    try {
      const jsonContent = JSON.stringify(data, null, 2);

      await photinoService.sendMessage<SettingsFileResponse>({ 
        module: 'SETTINGS', 
        type: 'SAVE_FILE', 
        payload: { filename, content: jsonContent } 
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
      await photinoService.sendMessage<SettingsFileResponse>({ 
        module: 'SETTINGS', 
        type: 'DELETE_FILE', 
        payload: { filename } 
      });

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
      const response = await photinoService.sendMessage<SettingsFileExistsResponse>({ 
        module: 'SETTINGS', 
        type: 'FILE_EXISTS', 
        payload: { filename } 
      });

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
      const response = await photinoService.sendMessage<SettingsFileListResponse>({ 
        module: 'SETTINGS', 
        type: 'LIST_FILES', 
        payload: {} 
      });

      return response.files || [];
    } catch (error) {
      console.error('[SettingsFileService] Failed to list settings files:', error);
      return [];
    }
  }
}

export const settingsFileService = new SettingsFileService();

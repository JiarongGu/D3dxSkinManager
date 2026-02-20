import { photinoService } from './photinoService';
import { LanguageSettings } from '../types/language.types';

/**
 * Service for managing language/i18n operations
 */
export const languageService = {
  /**
   * Get language file by code
   */
  async getLanguage(languageCode: string): Promise<LanguageSettings | null> {
    try {
      const response = await photinoService.sendMessage<{ success: boolean; language?: LanguageSettings }>({
        module: 'SETTINGS',
        type: 'GET_LANGUAGE',
        payload: { languageCode },
      });

      if (response.success && response.language) {
        return response.language;
      }

      return null;
    } catch (error) {
      console.error('[languageService] Failed to get language:', error);
      throw error;
    }
  },

  /**
   * Get all available language codes
   */
  async getAvailableLanguages(): Promise<string[]> {
    try {
      const response = await photinoService.sendMessage<{ success: boolean; languages: string[] }>({
        module: 'SETTINGS',
        type: 'GET_AVAILABLE_LANGUAGES',
        payload: {},
      });

      if (response.success && response.languages) {
        return response.languages;
      }

      return [];
    } catch (error) {
      console.error('[languageService] Failed to get available languages:', error);
      return [];
    }
  },

  /**
   * Check if language exists
   */
  async languageExists(languageCode: string): Promise<boolean> {
    try {
      const response = await photinoService.sendMessage<{ exists: boolean }>({
        module: 'SETTINGS',
        type: 'LANGUAGE_EXISTS',
        payload: { languageCode },
      });

      return response.exists;
    } catch (error) {
      console.error('[languageService] Failed to check language existence:', error);
      return false;
    }
  },
};

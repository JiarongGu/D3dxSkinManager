import { BaseModuleService } from '../baseModuleService';
import { LanguageSettings } from '../../types/language.types';

/**
 * Service for managing language/i18n operations
 */
export class LanguageService extends BaseModuleService {
  constructor() {
    super('SETTING');
  }

  /**
   * Get language file by code
   */
  async getLanguage(languageCode: string): Promise<LanguageSettings | undefined> {
    try {
      const response = await this.sendMessage<{ success: boolean; language?: LanguageSettings }>(
        'GET_LANGUAGE',
        undefined,
        { languageCode }
      );

      if (response.success && response.language) {
        return response.language;
      }

      return undefined;
    } catch (error: unknown) {
            throw error;
    }
  }

  /**
   * Get all available language codes
   */
  async getAvailableLanguages(): Promise<string[]> {
    try {
      const response = await this.sendMessage<{ success: boolean; languages: string[] }>(
        'GET_AVAILABLE_LANGUAGES',
        undefined,
        {}
      );

      if (response.success && response.languages) {
        return response.languages;
      }

      return [];
    } catch (error: unknown) {
            return [];
    }
  }

  /**
   * Check if language exists
   */
  async languageExists(languageCode: string): Promise<boolean> {
    try {
      const response = await this.sendMessage<{ exists: boolean }>(
        'LANGUAGE_EXISTS',
        undefined,
        { languageCode }
      );

      return response.exists;
    } catch (error: unknown) {
            return false;
    }
  }
}
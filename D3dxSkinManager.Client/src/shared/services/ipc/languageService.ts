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

}
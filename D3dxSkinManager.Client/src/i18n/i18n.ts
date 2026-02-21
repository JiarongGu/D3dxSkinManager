import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { languageService } from '../shared/services/languageService';
import { DEFAULT_LANGUAGE } from '../shared/types/language.types';
import logger from '../shared/utils/logger';

/**
 * Custom backend for i18next that loads translations from our backend service
 */
const customBackend = {
  type: 'backend' as const,
  init: () => {},
  read: async (language: string, namespace: string, callback: (error: Error | null, data?: any) => void) => {
    try {
      logger.info(`[i18n] Loading language: ${language}, namespace: ${namespace}`);
      const languageSettings = await languageService.getLanguage(language);

      if (languageSettings && languageSettings.translations) {
        callback(null, languageSettings.translations);
      } else {
        callback(new Error(`Language ${language} not found`));
      }
    } catch (error) {
      logger.error(`[i18n] Failed to load language ${language}:`, error);
      callback(error as Error);
    }
  },
};

/**
 * Initialize i18next with custom backend
 */
i18n
  .use(customBackend)
  .use(initReactI18next)
  .init({
    lng: DEFAULT_LANGUAGE,
    fallbackLng: DEFAULT_LANGUAGE,
    debug: process.env.NODE_ENV === 'development',

    // Default namespace
    ns: ['translation'],
    defaultNS: 'translation',

    interpolation: {
      escapeValue: false, // React already escapes values
    },

    react: {
      useSuspense: false, // Disable suspense for now
    },

    // Load only current language
    load: 'currentOnly',

    // Custom options
    backend: {
      loadPath: '{{lng}}', // This will be passed to our custom backend
    },
  });

export default i18n;

/**
 * Load language from backend settings and set it in i18next
 */
export const loadLanguageFromSettings = async () => {
  try {
    const { settingsService } = await import('../modules/settings/services/settingsService');
    const settings = await settingsService.getGlobalSettings();
    const savedLanguage = settings.language || DEFAULT_LANGUAGE;

    logger.info(`[i18n] Loading saved language: ${savedLanguage}`);
    await i18n.changeLanguage(savedLanguage);

    return savedLanguage;
  } catch (error) {
    logger.error('[i18n] Failed to load language from settings:', error);
    return DEFAULT_LANGUAGE;
  }
};

/**
 * Change language and save to backend
 */
export const changeLanguage = async (language: string) => {
  try {
    logger.info(`[i18n] Changing language to: ${language}`);

    // Change language in i18next
    await i18n.changeLanguage(language);

    // Save to backend settings
    const { settingsService } = await import('../modules/settings/services/settingsService');
    await settingsService.updateGlobalSetting('language', language);

    logger.info(`[i18n] Language changed successfully`);
  } catch (error) {
    logger.error('[i18n] Failed to change language:', error);
    throw error;
  }
};

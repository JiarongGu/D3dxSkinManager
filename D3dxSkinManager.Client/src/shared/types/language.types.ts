/**
 * Language settings types for i18n support
 */

export interface LanguageSettings {
  code: string;
  name: string;
  translations: TranslationDictionary;
}

export interface TranslationDictionary {
  [key: string]: string | TranslationDictionary;
}

export interface AvailableLanguage {
  code: string;
  name: string;
}

export const AVAILABLE_LANGUAGES: AvailableLanguage[] = [
  { code: 'en', name: 'English' },
  { code: 'cn', name: '中文' },
];

export const DEFAULT_LANGUAGE = 'en';

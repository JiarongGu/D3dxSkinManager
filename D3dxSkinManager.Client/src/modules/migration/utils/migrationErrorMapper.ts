import type { TFunction } from 'i18next';
import type { MigrationError } from '../services/migrationService';

/**
 * Converts backend error codes to i18n keys
 * Follows the pattern: SNAKE_CASE -> camelCase
 */
const codeToI18nKey = (code: string): string => {
  return code
    .toLowerCase()
    .replace(/_([a-z])/g, (_, letter) => letter.toUpperCase());
};

/**
 * Maps error category code to i18n key
 * Example: "MOD_MIGRATION" -> "migration.error.category.modMigration"
 */
export const getCategoryI18nKey = (categoryCode?: string): string => {
  if (!categoryCode) return 'migration.progress.generalErrors';
  return `migration.error.category.${codeToI18nKey(categoryCode)}`;
};

/**
 * Maps error step code to i18n key
 * Example: "MIGRATE_MOD_ARCHIVES" -> "migration.error.step.migrateModArchives"
 */
export const getStepI18nKey = (stepCode?: string): string | null => {
  if (!stepCode) return null;
  return `migration.error.step.${codeToI18nKey(stepCode)}`;
};

/**
 * Maps error message code to i18n key
 * Example: "MOD_MIGRATION_FAILED" -> "migration.error.code.modMigrationFailed"
 */
export const getMessageI18nKey = (messageCode?: string): string | null => {
  if (!messageCode) return null;
  return `migration.error.code.${codeToI18nKey(messageCode)}`;
};

/**
 * Gets translated mod name for the error
 * If modName exists, use it. Otherwise, generate from modSha if available
 */
export const getTranslatedModName = (
  error: MigrationError,
  t: TFunction
): string => {
  if (error.modName) {
    return error.modName;
  }

  if (error.modSha) {
    // Use translation with parameter
    return t('migration.error.previewFor', { sha: error.modSha });
  }

  return t('migration.progress.generalErrors');
};

/**
 * Gets translated category text
 */
export const getTranslatedCategory = (
  categoryCode?: string,
  t?: TFunction
): string | undefined => {
  if (!categoryCode || !t) return undefined;
  const key = getCategoryI18nKey(categoryCode);
  return t(key);
};

/**
 * Gets translated step text
 */
export const getTranslatedStep = (
  stepCode?: string,
  t?: TFunction
): string | undefined => {
  if (!stepCode || !t) return undefined;
  const key = getStepI18nKey(stepCode);
  return key ? t(key) : undefined;
};

/**
 * Gets a user-friendly error message
 * Tries to use messageCode translation first, falls back to raw message
 */
export const getTranslatedMessage = (
  error: MigrationError,
  t: TFunction
): string => {
  // Try to get translated message from code
  if (error.messageCode) {
    const key = getMessageI18nKey(error.messageCode);
    if (key) {
      const translated = t(key, error.parameters || {});
      // If translation key exists (not returning the key itself), use it
      if (translated !== key) {
        return translated;
      }
    }
  }

  // Fall back to raw error message
  return error.message;
};

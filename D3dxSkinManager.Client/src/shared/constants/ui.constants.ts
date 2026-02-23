/**
 * UI Constants
 *
 * This file contains constant values used throughout the UI that don't require
 * internationalization (i18n). These are technical values that remain the same
 * regardless of language - typically paths, technical syntax, and proper nouns.
 */

/**
 * Example file paths (technical syntax, doesn't need translation)
 */
export const PATH_PLACEHOLDERS = {
  GAME_EXE: 'C:\\Program Files\\Game\\game.exe',
  CUSTOM_PROGRAM: 'C:\\Programs\\CustomTool\\tool.exe',
} as const;

/**
 * Command-line argument examples (technical syntax)
 */
export const LAUNCH_ARG_EXAMPLES = {
  COMMON_ARGS: '-windowed -dx11',
} as const;

/**
 * Screen resolution presets (technical specifications)
 */
export const SCREEN_RESOLUTIONS = {
  HD: { width: 1280, height: 720, label: '1280×720 (HD)' },
  FULL_HD: { width: 1920, height: 1080, label: '1920×1080 (Full HD)' },
  '2K': { width: 2560, height: 1440, label: '2560×1440 (2K)' },
  '4K': { width: 3840, height: 2160, label: '3840×2160 (4K)' },
} as const;

/**
 * Default resolution values (technical numbers)
 */
export const RESOLUTION_DEFAULTS = {
  WIDTH: '1920',
  HEIGHT: '1080',
} as const;

/**
 * Module/Framework Names (proper nouns, brand names)
 */
export const MODULE_NAMES = {
  UNITY: 'Unity',
  MIGOTO: '3DMigoto',
} as const;

/**
 * Consolidated IPC Service API
 *
 * All IPC service implementations are in ./ipc/ folder
 */

// Re-export all services, types, and enums
export * from './modService';
export * from './profileService';
export * from './workflowService';
export * from './launchService';
export * from './settingsService';
export * from './categoryService';
export * from './languageService';
export * from './systemService';
export * from './toolService';

// Import classes for creating singleton instances
import { ModService } from './modService';
import { ProfileService } from './profileService';
import { WorkflowService } from './workflowService';
import { LaunchService } from './launchService';
import { SettingsService } from './settingsService';
import { CategoryService } from './categoryService';
import { LanguageService } from './languageService';
import { SystemService } from './systemService';
import { ToolService } from './toolService';

/**
 * Consolidated API with all IPC services
 */
export const api = {
  mod: new ModService(),
  profile: new ProfileService(),
  workflow: new WorkflowService(),
  launch: new LaunchService(),
  settings: new SettingsService(),
  category: new CategoryService(),
  language: new LanguageService(),
  system: new SystemService(),
  tool: new ToolService(),
} as const;

// Legacy exports for backward compatibility
export const modService = api.mod;
export const profileService = api.profile;
export const workflowService = api.workflow;
export const launchService = api.launch;
export const settingsService = api.settings;
export const categoryService = api.category;
export const languageService = api.language;
export const systemService = api.system;
export const toolService = api.tool;

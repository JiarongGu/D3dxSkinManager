import { BaseModuleService } from '../baseModuleService';
import {
  Profile,
  CreateProfileRequest,
  UpdateProfileRequest,
  ProfileSwitchResult,
  ProfileListResponse
} from '../../types/profile.types';

/**
 * Mod Work Configuration Model (work directory with flattened cache cleanup)
 */
export interface ModWorkConfiguration {
  // internal = app-managed work folder; external = custom folder; xxmi = an XXMI importer folder.
  mode: 'internal' | 'external' | 'xxmi';
  directory?: string;
  cleanupEnabled: boolean;
  cleanupMaxCaches: number;
  internalDirectory?: string; // Computed by backend, for display only - not persisted
}

/**
 * Mod Import Configuration Model
 */
export interface ModImportConfiguration {
  compressionType: '7z' | 'zip' | 'rar';
  compressionMode: 'fast' | 'high' | 'ultra';
}

/**
 * Tab-specific settings
 */
export interface TabSettings {
  mod: {
    panelSize: string; // Format: "categoryWidth modListWidth" (e.g., "25 40")
    categoryViewMode?: string; // "tree" or "grid", default: "tree"
    lockedExpandedCategories?: string[]; // Category IDs that are locked expanded
  };
}

/**
 * Profile Configuration Model (stored in {profileId}/config.json)
 * Note: Windows and Tabs come from other update events
 */
export interface LaunchConfiguration {
  path: string;
  args: string;
}

export interface ModFixConfiguration {
  pythonPath: string;
  timeoutMinutes: number;
  supportedExtensions: string[];
  autoConfirm: boolean;
}

export interface ProfileConfiguration {
  profileId: string;
  modWork: ModWorkConfiguration;
  modImport: ModImportConfiguration;
  tabs: TabSettings;
  launch?: LaunchConfiguration;
  fixTools?: ModFixConfiguration;
  windows?: Record<string, any>; // Updated via other events
}

/**
 * Service for managing mod management profiles
 * Provides type-safe communication with the PROFILE module backend
 */
export class ProfileService extends BaseModuleService {
  constructor() {
    super('PROFILE');
  }

  /**
   * Get all profiles with active profile ID
   */
  async getAllProfiles(): Promise<ProfileListResponse> {
    return this.sendMessage<ProfileListResponse>('GET_ALL');
  }

  /**
   * Get currently active profile
   */
  async getActiveProfile(): Promise<Profile | undefined> {
    return this.sendOptionalMessage<Profile>('GET_ACTIVE');
  }

  /**
   * Get profile by ID
   */
  async getProfileById(profileId: string): Promise<Profile | undefined> {
    return this.sendOptionalMessage<Profile>('GET_BY_ID', undefined, { profileId });
  }

  /**
   * Create a new profile
   */
  async createProfile(request: CreateProfileRequest): Promise<Profile> {
    return this.sendMessage<Profile>('CREATE', undefined, {
      name: request.name,
      description: request.description,
      color: request.color,
      gameName: request.gameName,
      thumbnailPath: request.thumbnailPath
    });
  }

  /**
   * Update profile metadata
   */
  async updateProfile(request: UpdateProfileRequest): Promise<boolean> {
    return this.sendBooleanMessage('UPDATE', undefined, {
      profileId: request.profileId,
      name: request.name,
      description: request.description,
      color: request.color,
      gameName: request.gameName,
      thumbnailPath: request.thumbnailPath
    });
  }

  /**
   * Delete a profile (cannot delete active profile)
   */
  async deleteProfile(profileId: string): Promise<boolean> {
    return this.sendBooleanMessage('DELETE', undefined, { profileId });
  }

  /**
   * Switch to a different profile
   */
  async switchProfile(profileId: string): Promise<ProfileSwitchResult> {
    return this.sendMessage<ProfileSwitchResult>('SWITCH', undefined, { profileId });
  }

  /**
   * Duplicate a profile
   */
  async duplicateProfile(sourceProfileId: string, newName: string): Promise<Profile> {
    return this.sendMessage<Profile>('DUPLICATE', undefined, { sourceProfileId, newName });
  }

  /**
   * Export profile configuration to JSON
   */
  async exportProfileConfig(profileId: string): Promise<string> {
    return this.sendMessage<string>('EXPORT_CONFIG', undefined, { profileId });
  }

  /**
   * Get profile configuration
   */
  async getProfileConfig(profileId: string): Promise<ProfileConfiguration | undefined> {
    return this.sendOptionalMessage<ProfileConfiguration>('GET_CONFIG', undefined, { profileId });
  }

  /**
   * Get profile configuration (alias for getProfileConfig for consistency)
   */
  async getProfileConfiguration(profileId: string): Promise<ProfileConfiguration | undefined> {
    return this.getProfileConfig(profileId);
  }

  /**
   * Update profile configuration
   */
  async updateProfileConfig(params: {
    profileId: string;
    workMode?: ModWorkConfiguration['mode'];
    workDirectory?: string;
    cleanupEnabled?: boolean;
    cleanupMaxCaches?: number;
    compressionType?: ModImportConfiguration['compressionType'];
    compressionMode?: ModImportConfiguration['compressionMode'];
    launchPath?: string;
    launchArgs?: string;
    fixToolsPythonPath?: string;
    fixToolsTimeoutMinutes?: number;
    fixToolsExtensions?: string[];
    fixToolsAutoConfirm?: boolean;
  }): Promise<boolean> {
    return this.sendBooleanMessage('UPDATE_CONFIG', undefined, {
      profileId: params.profileId,
      workMode: params.workMode,
      workDirectory: params.workDirectory,
      cleanupEnabled: params.cleanupEnabled,
      cleanupMaxCaches: params.cleanupMaxCaches,
      compressionType: params.compressionType,
      compressionMode: params.compressionMode,
      launchPath: params.launchPath,
      launchArgs: params.launchArgs,
      fixToolsPythonPath: params.fixToolsPythonPath,
      fixToolsTimeoutMinutes: params.fixToolsTimeoutMinutes,
      fixToolsExtensions: params.fixToolsExtensions,
      fixToolsAutoConfirm: params.fixToolsAutoConfirm,
    });
  }

  /**
   * Update mod panel size in profile config
   */
  async updateModPanelSize(profileId: string, panelSize: string): Promise<{ success: boolean; config?: ProfileConfiguration }> {
    return this.sendMessage('UPDATE_MOD_PANEL_SIZE', undefined, { profileId, panelSize });
  }

  /**
   * Update category view mode in profile config
   */
  async updateCategoryViewMode(profileId: string, viewMode: string): Promise<{ success: boolean; config?: ProfileConfiguration }> {
    return this.sendMessage('UPDATE_CATEGORY_VIEW_MODE', undefined, { profileId, viewMode });
  }

  /**
   * Update locked expanded categories in profile config
   */
  async updateLockedCategories(profileId: string, lockedCategories: string[]): Promise<{ success: boolean }> {
    return this.sendMessage('UPDATE_LOCKED_CATEGORIES', undefined, { profileId, lockedCategories });
  }

}

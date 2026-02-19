import { BaseModuleService } from '../../../shared/services/baseModuleService';
import {
  Profile,
  CreateProfileRequest,
  UpdateProfileRequest,
  ProfileSwitchResult,
  ProfileListResponse
} from '../../../shared/types/profile.types';

/**
 * Profile Configuration Model
 */
export interface ProfileConfiguration {
  profileId: string;
  archiveHandlingMode: string;
  defaultGrading: string;
  autoGenerateThumbnails: boolean;
  autoClassifyMods: boolean;
  classificationPatterns?: string;
  thumbnailAlgorithm: string;
  migotoVersion: string;
  gamePath?: string;
  gameLaunchArgs?: string;
  customProgramPath?: string;
  customProgramArgs?: string;
  customSettings?: string;
}

/**
 * Service for managing mod management profiles
 * Provides type-safe communication with the PROFILE module backend
 */
class ProfileService extends BaseModuleService {
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
  async getActiveProfile(): Promise<Profile | null> {
    return this.sendNullableMessage<Profile>('GET_ACTIVE');
  }

  /**
   * Get profile by ID
   */
  async getProfileById(profileId: string): Promise<Profile | null> {
    return this.sendNullableMessage<Profile>('GET_BY_ID', undefined, { profileId });
  }

  /**
   * Create a new profile
   */
  async createProfile(request: CreateProfileRequest): Promise<Profile> {
    return this.sendMessage<Profile>('CREATE', undefined, {
      name: request.name,
      description: request.description,
      workDirectory: request.workDirectory,
      colorTag: request.colorTag,
      iconName: request.iconName,
      gameName: request.gameName,
      copyFromCurrent: request.copyFromCurrent
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
      workDirectory: request.workDirectory,
      colorTag: request.colorTag,
      iconName: request.iconName,
      gameName: request.gameName
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
  async getProfileConfig(profileId: string): Promise<ProfileConfiguration | null> {
    return this.sendNullableMessage<ProfileConfiguration>('GET_CONFIG', undefined, { profileId });
  }

  /**
   * Update profile configuration
   */
  async updateProfileConfig(config: Partial<ProfileConfiguration> & { profileId: string }): Promise<boolean> {
    return this.sendBooleanMessage('UPDATE_CONFIG', undefined, {
      profileId: config.profileId,
      archiveHandlingMode: config.archiveHandlingMode,
      defaultGrading: config.defaultGrading,
      autoGenerateThumbnails: config.autoGenerateThumbnails,
      autoClassifyMods: config.autoClassifyMods
    });
  }

  /**
   * Format bytes to human-readable string
   */
  formatBytes(bytes: number): string {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return Math.round((bytes / Math.pow(k, i)) * 100) / 100 + ' ' + sizes[i];
  }
}

// Export singleton instance
export const profileService = new ProfileService();

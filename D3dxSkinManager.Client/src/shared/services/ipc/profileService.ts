import { BaseModuleService } from '../baseModuleService';
import {
  Profile,
  CreateProfileRequest,
  UpdateProfileRequest,
  ProfileSwitchResult,
  ProfileListResponse
} from '../../types/profile.types';

/**
 * Work Directory Configuration Model
 */
export interface WorkDirectoryConfiguration {
  mode: string;
  directory?: string;
  internalWorkDirectory?: string; // Computed by backend, for display only
}

/**
 * Profile Configuration Model (stored in {profileId}/config.json)
 * Simplified: only contains profileId, migotoVersion, and work directory
 */
export interface ProfileConfiguration {
  profileId: string;
  migotoVersion: string;
  work: WorkDirectoryConfiguration;
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
   * Update profile configuration
   */
  async updateProfileConfig(config: Partial<ProfileConfiguration> & { profileId: string }): Promise<boolean> {
    return this.sendBooleanMessage('UPDATE_CONFIG', undefined, {
      profileId: config.profileId,
      migotoVersion: config.migotoVersion,
      workMode: config.work?.mode,
      workDirectory: config.work?.directory
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

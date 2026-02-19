/**
 * Profile model - matches backend Profile.cs
 */
export interface Profile {
  id: string;
  name: string;
  description?: string;
  gameDirectory?: string;
  workDirectory: string;
  dataDirectory: string;
  isActive: boolean;
  createdAt: string;
  lastUsedAt: string;
  modCount?: number;
  totalSize?: number;
  colorTag?: string;
  iconName?: string;
  gameName?: string;
}

/**
 * Request to create a new profile
 */
export interface CreateProfileRequest {
  name: string;
  description?: string;
  gameDirectory?: string;
  workDirectory?: string;
  colorTag?: string;
  iconName?: string;
  gameName?: string;
  copyFromCurrent?: boolean;
}

/**
 * Request to update profile metadata
 */
export interface UpdateProfileRequest {
  profileId: string;
  name?: string;
  description?: string;
  gameDirectory?: string;
  workDirectory?: string;
  colorTag?: string;
  iconName?: string;
  gameName?: string;
}

/**
 * Result of switching profiles
 */
export interface ProfileSwitchResult {
  success: boolean;
  activeProfile: Profile;
  message?: string;
}

/**
 * Response when getting all profiles
 */
export interface ProfileListResponse {
  profiles: Profile[];
  activeProfileId: string;
}

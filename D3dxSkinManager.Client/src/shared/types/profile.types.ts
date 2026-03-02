/**
 * Profile model - matches backend Profile.cs
 * Simplified structure: profiles.json stores only id, name, description, color, gameName, thumbnail
 */
export interface Profile {
  id: string;
  name: string;
  description?: string;
  color?: string;
  gameName?: string;
  thumbnail?: string;
}

/**
 * Request to create a new profile
 */
export interface CreateProfileRequest {
  name: string;
  description?: string;
  color?: string;
  gameName?: string;
  thumbnailPath?: string;
}

/**
 * Request to update profile metadata
 */
export interface UpdateProfileRequest {
  profileId: string;
  name?: string;
  description?: string;
  color?: string;
  gameName?: string;
  thumbnailPath?: string;
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

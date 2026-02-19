import { profileService, ProfileConfiguration } from './profileService';

// Re-export ProfileConfiguration for convenience
export type { ProfileConfiguration };

/**
 * Get configuration for a specific profile
 */
export const getProfileConfig = async (profileId: string): Promise<ProfileConfiguration | null> => {
  try {
    return await profileService.getProfileConfig(profileId);
  } catch (error) {
    console.error('Failed to get profile configuration:', error);
    throw error;
  }
};

/**
 * Get configuration for the currently selected profile
 * @deprecated Use getProfileConfig with explicit profileId instead
 */
export const getActiveProfileConfig = async (profileId?: string): Promise<ProfileConfiguration | null> => {
  try {
    if (!profileId) {
      throw new Error('Profile ID is required');
    }

    // Get its configuration
    return await profileService.getProfileConfig(profileId);
  } catch (error) {
    console.error('Failed to get profile configuration:', error);
    throw error;
  }
};

/**
 * Update profile configuration
 */
export const updateProfileConfig = async (
  profileId: string,
  updates: Partial<Omit<ProfileConfiguration, 'profileId'>>
): Promise<boolean> => {
  try {
    return await profileService.updateProfileConfig({
      profileId,
      ...updates
    });
  } catch (error) {
    console.error('Failed to update profile configuration:', error);
    throw error;
  }
};

/**
 * Update a single configuration field for the specified profile
 */
export const updateActiveProfileConfigField = async (
  profileId: string,
  field: keyof Omit<ProfileConfiguration, 'profileId'>,
  value: any
): Promise<boolean> => {
  try {
    if (!profileId) {
      throw new Error('Profile ID is required');
    }

    // Update the field
    return await updateProfileConfig(profileId, { [field]: value });
  } catch (error) {
    console.error(`Failed to update profile config field ${field}:`, error);
    throw error;
  }
};

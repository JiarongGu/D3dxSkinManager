import { createContext, useContext, useState, useEffect, ReactNode } from 'react';
import { Profile, ProfileSwitchResult } from '../types/profile.types';
import { profileService } from '../../modules/profiles/services/profileService';

/**
 * Profile context state
 * In the stateless architecture, we don't have an "active" profile on the backend.
 * The frontend maintains the selected profile and includes it in every request.
 */
interface ProfileState {
  selectedProfile: Profile | null;
  profiles: Profile[];
  loading: boolean;
  error: string | null;
}

/**
 * Profile context value
 */
interface ProfileContextValue {
  state: ProfileState;
  selectedProfile: Profile | null;
  selectedProfileId: string | undefined;
  profiles: Profile[];
  loading: boolean;
  error: string | null;
  actions: {
    setSelectedProfile: (profile: Profile) => void;
    loadProfiles: () => Promise<void>;
    selectProfile: (profileId: string) => Promise<void>;
    createProfile: (name: string, description?: string) => Promise<Profile>;
    updateProfile: (profileId: string, name: string, description?: string) => Promise<void>;
    deleteProfile: (profileId: string) => Promise<void>;
  };
}

const ProfileContext = createContext<ProfileContextValue | undefined>(undefined);

/**
 * Profile provider props
 */
interface ProfileProviderProps {
  children: ReactNode;
  initialProfile?: Profile;
}

/**
 * Profile provider component
 *
 * Manages the currently selected profile for the frontend.
 * In the stateless backend architecture:
 * - There is no "active" profile on the backend
 * - Each request must include the profileId
 * - The frontend maintains the selected profile state
 */
export function ProfileProvider({ children, initialProfile }: ProfileProviderProps) {
  const [state, setState] = useState<ProfileState>({
    selectedProfile: initialProfile || null,
    profiles: [],
    loading: false,
    error: null
  });

  // Log when profile changes
  useEffect(() => {
    if (state.selectedProfile) {
      console.log('[ProfileContext] Selected profile set:', state.selectedProfile.name, state.selectedProfile.id);
    }
  }, [state.selectedProfile]);

  /**
   * Set the selected profile
   */
  const setSelectedProfile = (profile: Profile) => {
    setState(prev => ({
      ...prev,
      selectedProfile: profile,
      error: null
    }));
  };

  /**
   * Load all profiles (no profileId needed for this request)
   */
  const loadProfiles = async () => {
    try {
      setState(prev => ({ ...prev, loading: true, error: null }));
      const response = await profileService.getAllProfiles();
      setState(prev => ({
        ...prev,
        profiles: response.profiles,
        loading: false
      }));
    } catch (error) {
      console.error('Failed to load profiles:', error);
      setState(prev => ({
        ...prev,
        error: 'Failed to load profiles',
        loading: false
      }));
    }
  };

  /**
   * Select a profile (frontend state change only)
   * The backend doesn't maintain an active profile
   */
  const selectProfile = async (profileId: string) => {
    try {
      setState(prev => ({ ...prev, loading: true, error: null }));

      // Find the profile in our list
      const profile = state.profiles.find(p => p.id === profileId);
      if (!profile) {
        // Load the profile if not in our list
        const loadedProfile = await profileService.getProfileById(profileId);
        if (!loadedProfile) {
          throw new Error('Profile not found');
        }
        setSelectedProfile(loadedProfile);
      } else {
        setSelectedProfile(profile);
      }

      // Note: We don't call "switchProfile" on the backend anymore
      // because the backend is stateless. We just update our local state.

      // Optionally update last used timestamp
      try {
        const result: ProfileSwitchResult = await profileService.switchProfile(profileId);
        if (!result.success) {
          console.warn('Failed to update profile timestamp:', result.message);
        }
      } catch (error) {
        // Non-critical - just updating timestamp
        console.warn('Failed to update profile timestamp:', error);
      }

      setState(prev => ({ ...prev, loading: false }));
    } catch (error) {
      console.error('Failed to select profile:', error);
      setState(prev => ({
        ...prev,
        error: 'Failed to select profile',
        loading: false
      }));
      throw error;
    }
  };

  /**
   * Create a new profile
   */
  const createProfile = async (name: string, description?: string): Promise<Profile> => {
    try {
      setState(prev => ({ ...prev, loading: true, error: null }));
      const profile = await profileService.createProfile({ name, description });

      // Add to our list
      setState(prev => ({
        ...prev,
        profiles: [...prev.profiles, profile],
        loading: false
      }));

      return profile;
    } catch (error) {
      console.error('Failed to create profile:', error);
      setState(prev => ({
        ...prev,
        error: 'Failed to create profile',
        loading: false
      }));
      throw error;
    }
  };

  /**
   * Update profile metadata
   */
  const updateProfile = async (profileId: string, name: string, description?: string) => {
    try {
      setState(prev => ({ ...prev, loading: true, error: null }));
      await profileService.updateProfile({ profileId, name, description });

      // Reload the profile
      const updated = await profileService.getProfileById(profileId);
      if (updated) {
        setState(prev => ({
          ...prev,
          profiles: prev.profiles.map(p => p.id === profileId ? updated : p),
          selectedProfile: prev.selectedProfile?.id === profileId ? updated : prev.selectedProfile,
          loading: false
        }));
      }
    } catch (error) {
      console.error('Failed to update profile:', error);
      setState(prev => ({
        ...prev,
        error: 'Failed to update profile',
        loading: false
      }));
      throw error;
    }
  };

  /**
   * Delete a profile
   */
  const deleteProfile = async (profileId: string) => {
    try {
      // Cannot delete the selected profile
      if (state.selectedProfile?.id === profileId) {
        throw new Error('Cannot delete the currently selected profile');
      }

      setState(prev => ({ ...prev, loading: true, error: null }));
      await profileService.deleteProfile(profileId);

      setState(prev => ({
        ...prev,
        profiles: prev.profiles.filter(p => p.id !== profileId),
        loading: false
      }));
    } catch (error) {
      console.error('Failed to delete profile:', error);
      setState(prev => ({
        ...prev,
        error: error instanceof Error ? error.message : 'Failed to delete profile',
        loading: false
      }));
      throw error;
    }
  };

  const value: ProfileContextValue = {
    state,
    selectedProfile: state.selectedProfile,
    selectedProfileId: state.selectedProfile?.id,
    profiles: state.profiles,
    loading: state.loading,
    error: state.error,
    actions: {
      setSelectedProfile,
      loadProfiles,
      selectProfile,
      createProfile,
      updateProfile,
      deleteProfile
    }
  };

  return (
    <ProfileContext.Provider value={value}>
      {children}
    </ProfileContext.Provider>
  );
}

/**
 * Hook to use profile context
 */
export function useProfile() {
  const context = useContext(ProfileContext);
  if (!context) {
    throw new Error('useProfile must be used within ProfileProvider');
  }
  return context;
}


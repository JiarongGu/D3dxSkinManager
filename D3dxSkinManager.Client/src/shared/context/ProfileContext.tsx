import { createContext, useContext, useState, useEffect, ReactNode } from 'react';
import { Profile, ProfileSwitchResult } from '../types/profile.types';
import { profileService } from '../services/ipc';

/**
 * Profile context state
 * In the stateless architecture, we don't have an "active" profile on the backend.
 * The frontend maintains the selected profile and includes it in every request.
 */
interface ProfileState {
  selectedProfile: Profile | undefined;
  profiles: Profile[];
  loading: boolean;
  error: string | undefined;
}

/**
 * Profile context value
 */
interface ProfileContextValue {
  state: ProfileState;
  selectedProfile: Profile | undefined;
  selectedProfileId: string | undefined;
  profiles: Profile[];
  loading: boolean;
  error: string | undefined;
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
    selectedProfile: initialProfile,
    profiles: [],
    loading: true, // Start in loading state
    error: undefined
  });

  // Initialize profiles on mount - only run once
  useEffect(() => {
    initializeProfiles();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Track when profile changes
  useEffect(() => {
    // Profile changed
  }, [state.selectedProfile]);

  /**
   * Initialize profiles and select the active one
   */
  const initializeProfiles = async () => {
    try {
      setState(prev => ({ ...prev, loading: true, error: undefined }));

      // Load all profiles
      const response = await profileService.getAllProfiles();
      const { profiles, activeProfileId } = response;

      // Find the active profile or default to the first one
      let profileToSelect = profiles.find(p => p.id === activeProfileId) || profiles[0];

      setState(prev => ({
        ...prev,
        profiles,
        selectedProfile: profileToSelect,
        loading: false
      }));
    } catch (error: unknown) {
      setState(prev => ({
        ...prev,
        error: 'Failed to initialize profiles',
        loading: false
      }));
    }
  };

  /**
   * Set the selected profile
   */
  const setSelectedProfile = (profile: Profile) => {
    setState(prev => ({
      ...prev,
      selectedProfile: profile,
      error: undefined
    }));
  };

  /**
   * Load all profiles (no profileId needed for this request)
   */
  const loadProfiles = async () => {
    try {
      setState(prev => ({ ...prev, loading: true, error: undefined }));
      const response = await profileService.getAllProfiles();
      setState(prev => ({
        ...prev,
        profiles: response.profiles,
        loading: false
      }));
    } catch (error: unknown) {
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
      setState(prev => ({ ...prev, loading: true, error: undefined }));

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
                  }
      } catch (error: unknown) {
        // Non-critical - just updating timestamp
              }

      setState(prev => ({ ...prev, loading: false }));
    } catch (error: unknown) {
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
      setState(prev => ({ ...prev, loading: true, error: undefined }));
      const profile = await profileService.createProfile({ name, description });

      // Add to our list
      setState(prev => ({
        ...prev,
        profiles: [...prev.profiles, profile],
        loading: false
      }));

      return profile;
    } catch (error: unknown) {
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
      setState(prev => ({ ...prev, loading: true, error: undefined }));
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
    } catch (error: unknown) {
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

      setState(prev => ({ ...prev, loading: true, error: undefined }));
      await profileService.deleteProfile(profileId);

      setState(prev => ({
        ...prev,
        profiles: prev.profiles.filter(p => p.id !== profileId),
        loading: false
      }));
    } catch (error: unknown) {
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


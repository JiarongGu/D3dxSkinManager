import {
  createContext,
  useContext,
  useState,
  useEffect,
  ReactNode,
  useCallback,
} from "react";
import { Profile, ProfileSwitchResult } from "../types/profile.types";
import { profileService } from "../services/ipc";
import { useDelayedLoading } from "../hooks/useDelayedLoading";

/**
 * Profile context state
 * In the stateless architecture, we don't have an "active" profile on the backend.
 * The frontend maintains the selected profile and includes it in every request.
 */
interface ProfileState {
  selectedProfile?: Profile;
  profiles: Profile[];
}

/**
 * Profile context value
 */
interface ProfileContextValue {
  selectedProfile?: Profile;
  selectedProfileId?: string;
  profiles: Profile[];
  loading: boolean;
  error?: string;
  actions: {
    setSelectedProfile: (profile: Profile) => void;
    loadProfiles: () => Promise<void>;
    selectProfile: (profileId: string) => Promise<void>;
    createProfile: (name: string, description?: string) => Promise<Profile>;
    updateProfile: (
      profileId: string,
      name: string,
      description?: string,
    ) => Promise<void>;
    deleteProfile: (profileId: string) => Promise<void>;
  };
}

const ProfileContext = createContext<ProfileContextValue | undefined>(
  undefined,
);

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
export function ProfileProvider({
  children,
  initialProfile,
}: ProfileProviderProps) {
  const { loading, execute, reset } = useDelayedLoading(200);
  const [error, setError] = useState<string>();
  const [state, setState] = useState<ProfileState>({
    selectedProfile: initialProfile,
    profiles: [],
  });

  // Initialize profiles on mount - only run once
  useEffect(() => {
     execute(async () => {
      try {
        setError(undefined);
        // Load all profiles
        const response = await profileService.getAllProfiles();
        const { profiles, activeProfileId } = response;

        // Find the active profile or default to the first one
        const profileToSelect = profiles.find((p) => p.id === activeProfileId) || profiles[0];

        setState((prev) => ({
          ...prev,
          profiles,
          selectedProfile: profileToSelect,
        }));
      } catch (error: unknown) {
        setError("Failed to initialize profiles");
      }
    });
    return () => reset();
  }, []);

  /**
   * Set the selected profile
   */
  const setSelectedProfile = useCallback((profile: Profile) => {
    setState((prev) => ({
      ...prev,
      selectedProfile: profile,
    }));
  }, []);

  /**
   * Load all profiles (no profileId needed for this request)
   */
  const loadProfiles = useCallback(async () => {
    execute(async () => {
      try {
        setError(undefined);
        const response = await profileService.getAllProfiles();
        setState((prev) => ({
          ...prev,
          profiles: response.profiles,
          loading: false,
        }));
      } catch (error: unknown) {
        setError("Failed to load profiles");
      }
    });
  }, []);

  /**
   * Select a profile (frontend state change only)
   * The backend doesn't maintain an active profile
   */
  const selectProfile = useCallback(async (profileId: string) => {
    execute(async () => {
      try {
        setError(undefined);
        // Find the profile in our list
        const profile = state.profiles.find((p) => p.id === profileId);
        if (!profile) {
          // Load the profile if not in our list
          const loadedProfile = await profileService.getProfileById(profileId);
          if (!loadedProfile) {
            throw new Error("Profile not found");
          }
          setSelectedProfile(loadedProfile);
        } else {
          setSelectedProfile(profile);
        }

        // Note: We don't call "switchProfile" on the backend anymore
        // because the backend is stateless. We just update our local state.

        // Optionally update last used timestamp
        try {
          const result: ProfileSwitchResult =
            await profileService.switchProfile(profileId);
          if (!result.success) {
          }
        } catch (error: unknown) {
          // Non-critical - just updating timestamp
        }
      } catch (error: unknown) {
        setError("Failed to select profile");
        throw error;
      }
    });
  }, []);

  /**
   * Create a new profile
   */
  const createProfile = useCallback(
    async (name: string, description?: string): Promise<Profile> => {
      return execute(async () => {
        try {
          setError(undefined);
          const profile = await profileService.createProfile({
            name,
            description,
          });

          // Add to our list
          setState((prev) => ({
            ...prev,
            profiles: [...prev.profiles, profile],
          }));

          return profile;
        } catch (error: unknown) {
          setError("Failed to create profile");
          throw error;
        }
      });
    },
    [],
  );

  /**
   * Update profile metadata
   */
  const updateProfile = async (
    profileId: string,
    name: string,
    description?: string,
  ) => {
    execute(async () => {
      try {
        setError(undefined);
        await profileService.updateProfile({ profileId, name, description });

        // Reload the profile
        const updated = await profileService.getProfileById(profileId);
        if (updated) {
          setState((prev) => ({
            ...prev,
            profiles: prev.profiles.map((p) =>
              p.id === profileId ? updated : p,
            ),
            selectedProfile:
              prev.selectedProfile?.id === profileId
                ? updated
                : prev.selectedProfile,
          }));
        }
      } catch (error: unknown) {
        setError("Failed to update profile");
        throw error;
      }
    });
  };

  /**
   * Delete a profile
   */
  const deleteProfile = useCallback(async (profileId: string) => {
    execute(async () => {
      try {
        // Cannot delete the selected profile
        if (state.selectedProfile?.id === profileId) {
          throw new Error("Cannot delete the currently selected profile");
        }
        setError(undefined);
        await profileService.deleteProfile(profileId);
        setState((prev) => ({
          ...prev,
          profiles: prev.profiles.filter((p) => p.id !== profileId),
        }));
      } catch (error: unknown) {
        setError("Failed to delete profile");
        throw error;
      }
    });
  }, []);

  const value: ProfileContextValue = {
    selectedProfile: state.selectedProfile,
    selectedProfileId: state.selectedProfile?.id,
    profiles: state.profiles,
    loading: loading,
    error: error,
    actions: {
      setSelectedProfile,
      loadProfiles,
      selectProfile,
      createProfile,
      updateProfile,
      deleteProfile,
    },
  };

  return (
    <ProfileContext.Provider value={value}>{children}</ProfileContext.Provider>
  );
}

/**
 * Hook to use profile context
 */
export function useProfile() {
  const context = useContext(ProfileContext);
  if (!context) {
    throw new Error("useProfile must be used within ProfileProvider");
  }
  return context;
}

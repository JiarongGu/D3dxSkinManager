import React, {
  createContext,
  useContext,
  useState,
  ReactNode,
  useCallback,
  useEffect,
} from "react";
import type { FormInstance } from "antd";
import { api } from "../../../../shared/services/ipc";
import { handleError } from "../../../../shared/utils/errorHandler";
import { notification } from "../../../../shared/utils/notification";
import { useProfile } from "../../../../shared/context/ProfileContext";
import { useDelayedLoading } from "../../../../shared/hooks/useDelayedLoading";
import { useStableRef } from "../../../../shared/hooks/useStableRef";
import type { ScreenCaptureProfile } from "../../../../shared/types/capture.types";
import { useTranslation } from "react-i18next";
import {
  eventBus,
  Module,
  ToolsEventType,
} from "../../../../shared/services/eventBus";

const NEW_PROFILE_ID = "__new__";

/**
 * Screen Capture context state
 */
interface ScreenCaptureContextState {
  // Profile management
  profiles: ScreenCaptureProfile[];
  selectedProfileId: string;
  isNewProfile: boolean;
  isDirty: boolean;

  // Name editing
  isEditingName: boolean;
  editingName: string;
  setEditingName: (name: string) => void;

  // Border overlay
  showingBorder: boolean;

  // Loading state
  loading: boolean;

  // Form instance
  form: FormInstance | undefined;
  setForm: (form: FormInstance) => void;

  // Actions
  loadProfiles: (selectProfileId?: string) => Promise<void>;
  handleProfileChange: (profileId: string | undefined) => void;
  handleSaveProfile: () => Promise<void>;
  handleDeleteProfile: () => Promise<void>;
  handleCancelEditName: () => void;
  handleToggleBorder: () => Promise<void>;
  handleCapture: () => Promise<void>;
  handleFormValuesChange: (changedValues: Record<string, unknown>) => void;
}

const ScreenCaptureContext = createContext<
  ScreenCaptureContextState | undefined
>(undefined);

/**
 * Hook to use screen capture context
 * Must be used within ScreenCaptureProvider
 */
export const useScreenCapture = (): ScreenCaptureContextState => {
  const context = useContext(ScreenCaptureContext);
  if (!context) {
    throw new Error(
      "useScreenCapture must be used within ScreenCaptureProvider",
    );
  }
  return context;
};

interface ScreenCaptureProviderProps {
  children: ReactNode;
}

/**
 * Screen Capture context provider
 * Manages state and operations for screen capture profiles
 */
export const ScreenCaptureProvider: React.FC<ScreenCaptureProviderProps> = ({
  children,
}) => {
  const { t } = useTranslation();
  const { selectedProfileId: currentProfileId } = useProfile();
  const { loading, execute } = useDelayedLoading(200);

  // State
  const [profiles, setProfiles] = useState<ScreenCaptureProfile[]>([]);
  const [selectedProfileId, setSelectedProfileId] =
    useState<string>(NEW_PROFILE_ID);
  const [isDirty, setIsDirty] = useState(false);
  const [isEditingName, setIsEditingName] = useState(false);
  const [editingName, setEditingName] = useState("");
  const [showingBorder, setShowingBorder] = useState(false);
  const [form, setForm] = useState<FormInstance>();

  // Stable refs
  const [currentProfileIdRef, selectedProfileIdRef, formRef] = useStableRef(
    currentProfileId,
    selectedProfileId,
    form,
  );

  // Computed
  const isNewProfile = selectedProfileId === NEW_PROFILE_ID;

  // Listen to bounds changes from overlay
  useEffect(() => {
    if (!form) return;

    const unsubscribe = eventBus.subscribe(
      Module.TOOL,
      ToolsEventType.CAPTURE_BOUNDS_CHANGED,
      (event) => {
        if (event.payload) {
          form.setFieldsValue({
            x: event.payload.x,
            y: event.payload.y,
            width: event.payload.width,
            height: event.payload.height,
          });
          setIsDirty(true);
        }
      },
    );

    return () => {
      unsubscribe();
    };
  }, [form]);

  // Reset profile state
  const resetProfile = useCallback((profileId?: string) => {
    setIsEditingName(false);
    setEditingName("");
    setSelectedProfileId(profileId ?? NEW_PROFILE_ID);
    setIsDirty(false);
  }, []);

  // Load profiles
  // Note: Uses refs which are stable, so no deps needed
  // No execute wrapper - this is an internal helper that can be called from within execute blocks
  const loadProfiles = useCallback(async (selectProfileId?: string) => {
    if (!currentProfileIdRef.current || !formRef.current) {
      return;
    }
    try {
      const data = await api.tool.getProfiles(currentProfileIdRef.current);
      setProfiles(data);

      // If a profile ID was provided, select it and load its values
      const profileToSelect = selectProfileId || selectedProfileIdRef.current;
      if (profileToSelect && profileToSelect !== NEW_PROFILE_ID) {
        const selectedProfile = data.find((p) => p.id === profileToSelect);
        if (selectedProfile) {
          formRef.current.setFieldsValue({
            x: selectedProfile.x,
            y: selectedProfile.y,
            width: selectedProfile.width,
            height: selectedProfile.height,
          });
        }
      }
    } catch (error) {
      handleError(error);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Initialize profiles on mount
  useEffect(() => {
    if (currentProfileId && form) {
      loadProfiles();

      // Set default form values
      api.system.getScreenResolution().then((resolution) => {
        form.setFieldsValue({
          x: resolution.width / 10,
          y: resolution.height / 10,
          width: resolution.width / 2,
          height: resolution.height / 2,
        });
      });
    }
  }, [currentProfileId, form, loadProfiles]);

  // Handle profile change
  const handleProfileChange = useCallback(
    (profileId: string | undefined) => {
      resetProfile(profileId);
      if (!profileId || profileId === NEW_PROFILE_ID || !form) {
        // Switching to <New Profile> - keep current form values
        return;
      }
      const profile = profiles.find((p) => p.id === profileId);
      if (profile) {
        form.setFieldsValue({
          x: profile.x,
          y: profile.y,
          width: profile.width,
          height: profile.height,
        });
      }
    },
    [profiles, resetProfile, form],
  );

  // Handle save profile
  const handleSaveProfile = useCallback(async () => {
    const isNew = selectedProfileIdRef.current === NEW_PROFILE_ID;

    // Workflow 1: New profile - prompt for name first
    if (isNew && !isEditingName) {
      setIsEditingName(true);
      setEditingName("");
      return;
    }

    // Now execute the actual save
    execute(async () => {
      if (!currentProfileIdRef.current || !form) {
        return;
      }

      const values = form.getFieldsValue();
      const captureData = {
        x: values.x ?? 0,
        y: values.y ?? 0,
        width: values.width ?? 1920,
        height: values.height ?? 1080,
      };

      try {
        // Workflow 1 continued: Create new profile with name
        if (isNew && isEditingName) {
          if (!editingName.trim()) {
            notification.warning(t("capture.enterName"), 1);
            return;
          }

          const createdProfileId = await api.tool.saveProfile(
            currentProfileIdRef.current,
            {
              name: editingName.trim(),
              ...captureData,
            },
          );

          await loadProfiles(createdProfileId);
          resetProfile(createdProfileId);
          return;
        }

        // Workflow 2: Update existing profile
        if (!isNew && selectedProfileIdRef.current) {
          const profile = profiles.find(
            (p) => p.id === selectedProfileIdRef.current,
          );
          if (!profile) return;

          await api.tool.saveProfile(currentProfileIdRef.current, {
            ...profile,
            ...captureData,
          });
          setIsDirty(false);
          await loadProfiles();
        }
      } catch (error) {
        handleError(error);
      }
    });
  }, [
    isEditingName,
    editingName,
    profiles,
    execute,
    form,
    loadProfiles,
    resetProfile,
    t,
    currentProfileIdRef,
    selectedProfileIdRef,
  ]);

  // Handle cancel edit name
  const handleCancelEditName = useCallback(() => {
    setIsEditingName(false);
    setEditingName("");
  }, []);

  // Handle delete profile
  const handleDeleteProfile = useCallback(async () => {
    if (
      !selectedProfileIdRef.current ||
      selectedProfileIdRef.current === NEW_PROFILE_ID
    ) {
      notification.warning(t("capture.noProfileSelected"), 1);
      return;
    }

    execute(async () => {
      if (!currentProfileIdRef.current || !form) {
        return;
      }

      // Save current form values before deleting
      const currentValues = form.getFieldsValue();

      try {
        await api.tool.deleteProfile(
          currentProfileIdRef.current,
          selectedProfileIdRef.current!,
        );

        await loadProfiles();
        resetProfile();
        // Keep the form values that were there before deletion
        form.setFieldsValue(currentValues);
      } catch (error) {
        handleError(error);
      }
    });
  }, [
    execute,
    form,
    loadProfiles,
    resetProfile,
    t,
    currentProfileIdRef,
    selectedProfileIdRef,
  ]);

  // Handle toggle border
  const handleToggleBorder = useCallback(async () => {
    execute(async () => {
      if (!currentProfileIdRef.current || !form) {
        notification.error(t("capture.noActiveProfile"), 1.5);
        return;
      }

      try {
        if (showingBorder) {
          await api.tool.hideBorder(currentProfileIdRef.current);
          setShowingBorder(false);
        } else {
          const values = form.getFieldsValue();
          const x = values.x ?? 0;
          const y = values.y ?? 0;
          const width = values.width ?? 1920;
          const height = values.height ?? 1080;

          await api.tool.showBorder(
            currentProfileIdRef.current,
            x,
            y,
            width,
            height,
          );
          setShowingBorder(true);
        }
      } catch (error) {
        handleError(error);
      }
    });
  }, [showingBorder, execute, form, t, currentProfileIdRef]);

  // Handle capture
  const handleCapture = useCallback(async () => {
    execute(async () => {
      if (!currentProfileIdRef.current || !form) {
        return;
      }

      try {
        const values = form.getFieldsValue();
        const x = values.x ?? 0;
        const y = values.y ?? 0;
        const width = values.width ?? 1920;
        const height = values.height ?? 1080;

        const result = await api.tool.captureScreen(
          currentProfileIdRef.current,
          {
            x,
            y,
            width,
            height,
            copyToClipboard: true,
            saveToFile: false,
          },
        );

        if (result.success) {
          notification.success(t("capture.captured"), 1);
        } else {
          notification.error(
            result.errorMessage || t("capture.captureFailed"),
            1.5,
          );
        }
      } catch (error) {
        handleError(error);
      }
    });
  }, [execute, form, t, currentProfileIdRef]);

  // Handle form values change
  const handleFormValuesChange = useCallback(
    async (_changedValues: Record<string, unknown>) => {
      // Mark form as dirty when values change (only for existing profiles)
      if (
        selectedProfileIdRef.current &&
        selectedProfileIdRef.current !== NEW_PROFILE_ID
      ) {
        setIsDirty(true);
      }

      // Only sync to border if border is currently showing
      if (!showingBorder || !currentProfileIdRef.current || !form) return;

      const values = form.getFieldsValue();
      const x = values.x ?? 0;
      const y = values.y ?? 0;
      const width = values.width ?? 1920;
      const height = values.height ?? 1080;

      try {
        await api.tool.showBorder(
          currentProfileIdRef.current,
          x,
          y,
          width,
          height,
        );
      } catch (error) {
        // Silently fail - non-critical border update
      }
    },
    [showingBorder, form, currentProfileIdRef, selectedProfileIdRef],
  );

  const value: ScreenCaptureContextState = {
    profiles,
    selectedProfileId,
    isNewProfile,
    isDirty,
    isEditingName,
    editingName,
    setEditingName,
    showingBorder,
    loading,
    form,
    setForm,
    loadProfiles,
    handleProfileChange,
    handleSaveProfile,
    handleDeleteProfile,
    handleCancelEditName,
    handleToggleBorder,
    handleCapture,
    handleFormValuesChange,
  };

  return (
    <ScreenCaptureContext.Provider value={value}>
      {children}
    </ScreenCaptureContext.Provider>
  );
};

export { NEW_PROFILE_ID };

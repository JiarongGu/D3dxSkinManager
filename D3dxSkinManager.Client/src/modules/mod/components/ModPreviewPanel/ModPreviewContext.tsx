import React, { createContext, useContext, useState, useEffect, useCallback } from 'react';
import { ModInfo } from '../../../../shared/types/mod.types';
import { modService } from '../../services/modService';
import { useProfile } from '../../../../shared/context/ProfileContext';
import { useModsStore } from '../../store/modsStore';
import { executeWithDelayedLoading } from '../../../../shared/utils/delayedLoading';
import { eventBus, Module, ModEventType } from '../../../../shared/services/eventBus';

interface ModViewState {
  currentMod: ModInfo | undefined;
  previewPaths: string[];
  currentPreviewIndex: number;
  cacheTimestamp: number; // Used to bust browser cache when images change
}

interface ModViewContextType {
  state: ModViewState;
  actions: {
    setCurrentMod: (mod: ModInfo | undefined) => void;
    setCurrentPreviewIndex: (index: number) => void;
    loadPreviewPaths: (sha: string) => Promise<void>;
    nextPreview: () => void;
    previousPreview: () => void;
  };
}

const ModViewContext = createContext<ModViewContextType | undefined>(undefined);

export const ModPreviewProvider: React.FC<{ children: React.ReactNode, mod: ModInfo | undefined }> = ({ children, mod }) => {
  const { state: profileState } = useProfile();
  const setPreviewLoading = useModsStore(s => s.setPreviewLoading);

  const [state, setState] = useState<ModViewState>({
    currentMod: mod,
    previewPaths: [],
    currentPreviewIndex: 0,
    cacheTimestamp: Date.now(),
  });

  useEffect(() => {
    setState((prev) => ({ ...prev, currentMod: mod }));
  }, [mod]);

  const loadPreviewPaths = useCallback(
    async (sha: string) => {
      const profileId = profileState.selectedProfile?.id;
      if (!profileId) {
        return;
      }

      // Check if preview is disabled for this mod (use the prop, not state)
      if (mod?.disablePreview) {
        // Clear previews when disabled
        setState((prev) => ({
          ...prev,
          previewPaths: [],
          currentPreviewIndex: 0,
        }));
        return;
      }

      try {
        await executeWithDelayedLoading(
          async () => {
            // Backend automatically imports from cache if no previews exist
            const paths = await modService.getPreviewPaths(profileId, sha);
            setState((prev) => ({
              ...prev,
              previewPaths: paths,
              currentPreviewIndex: 0,
              cacheTimestamp: Date.now(), // Bust browser cache
            }));
          },
          setPreviewLoading,
          100
        );
      } catch (error: unknown) {
                setState((prev) => ({
          ...prev,
          previewPaths: [],
          currentPreviewIndex: 0,
          cacheTimestamp: Date.now(), // Bust browser cache
        }));
      }
    },
    [profileState.selectedProfile?.id, setPreviewLoading, mod?.disablePreview]
  );

  // Load preview paths when mod changes, when isLoaded status changes, or when disablePreview changes
  useEffect(() => {
    if (mod?.sha && profileState.selectedProfile?.id) {
      loadPreviewPaths(mod.sha);
    } else {
      // Clear previews when no mod is selected
      setState((prev) => ({
        ...prev,
        previewPaths: [],
        currentPreviewIndex: 0,
      }));
    }
  }, [mod?.sha, mod?.isLoaded, mod?.disablePreview, profileState.selectedProfile?.id, loadPreviewPaths]);

  // Listen to PREVIEW_IMPORTED events to refresh when backend imports previews
  useEffect(() => {
    const unsubscribe = eventBus.subscribe(
      Module.MOD,
      ModEventType.PREVIEW_IMPORTED,
      (event) => {
        // Only reload if the event is for the currently displayed mod
        if (event.payload && state.currentMod?.sha === event.payload.sha) {
          loadPreviewPaths(state.currentMod.sha);
        }
      }
    );

    return unsubscribe;
  }, [state.currentMod?.sha, loadPreviewPaths]);

  const setCurrentMod = useCallback((mod: ModInfo | undefined) => {
    setState((prev) => ({
      ...prev,
      currentMod: mod,
      currentPreviewIndex: 0,
    }));
  }, []);

  const setCurrentPreviewIndex = useCallback((index: number) => {
    setState((prev) => ({
      ...prev,
      currentPreviewIndex: Math.max(0, Math.min(index, prev.previewPaths.length - 1)),
    }));
  }, []);

  const nextPreview = useCallback(() => {
    setState((prev) => ({
      ...prev,
      currentPreviewIndex: (prev.currentPreviewIndex + 1) % Math.max(1, prev.previewPaths.length),
    }));
  }, []);

  const previousPreview = useCallback(() => {
    setState((prev) => ({
      ...prev,
      currentPreviewIndex:
        (prev.currentPreviewIndex - 1 + prev.previewPaths.length) % Math.max(1, prev.previewPaths.length),
    }));
  }, []);

  const value: ModViewContextType = {
    state,
    actions: {
      setCurrentMod,
      setCurrentPreviewIndex,
      loadPreviewPaths,
      nextPreview,
      previousPreview,
    },
  };

  return <ModViewContext.Provider value={value}>{children}</ModViewContext.Provider>;
};

export const useModView = (): ModViewContextType => {
  const context = useContext(ModViewContext);
  if (!context) {
    throw new Error('useModView must be used within ModViewProvider');
  }
  return context;
};

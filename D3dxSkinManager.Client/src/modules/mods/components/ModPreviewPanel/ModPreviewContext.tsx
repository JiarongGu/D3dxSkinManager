import React, { createContext, useContext, useState, useEffect, useCallback } from 'react';
import { ModInfo } from '../../../../shared/types/mod.types';
import { modService } from '../../services/modService';
import { useProfile } from '../../../../shared/context/ProfileContext';

interface ModViewState {
  currentMod: ModInfo | null;
  previewPaths: string[];
  loadingPreviews: boolean;
  currentPreviewIndex: number;
  cacheTimestamp: number; // Used to bust browser cache when images change
}

interface ModViewContextType {
  state: ModViewState;
  actions: {
    setCurrentMod: (mod: ModInfo | null) => void;
    setCurrentPreviewIndex: (index: number) => void;
    loadPreviewPaths: (sha: string) => Promise<void>;
    nextPreview: () => void;
    previousPreview: () => void;
  };
}

const ModViewContext = createContext<ModViewContextType | undefined>(undefined);

export const ModPreviewProvider: React.FC<{ children: React.ReactNode, mod: ModInfo | null }> = ({ children, mod }) => {
  const { state: profileState } = useProfile();
  const [state, setState] = useState<ModViewState>({
    currentMod: mod,
    previewPaths: [],
    loadingPreviews: false,
    currentPreviewIndex: 0,
    cacheTimestamp: Date.now(),
  });

  useEffect(() => {
    setState((prev) => ({ ...prev, currentMod: mod }));
  }, [mod]);

  const loadPreviewPaths = useCallback(
    async (sha: string) => {
      if (!profileState.selectedProfile?.id) {
        return;
      }

      setState((prev) => ({ ...prev, loadingPreviews: true }));

      try {
        const paths = await modService.getPreviewPaths(
          profileState.selectedProfile.id,
          sha
        );
        setState((prev) => ({
          ...prev,
          previewPaths: paths,
          loadingPreviews: false,
          currentPreviewIndex: 0,
          cacheTimestamp: Date.now(), // Bust browser cache
        }));
      } catch (error) {
        console.error('Failed to load preview paths:', error);
        setState((prev) => ({
          ...prev,
          previewPaths: [],
          loadingPreviews: false,
          currentPreviewIndex: 0,
          cacheTimestamp: Date.now(), // Bust browser cache
        }));
      }
    },
    [profileState.selectedProfile?.id]
  );

  // Load preview paths when mod changes
  useEffect(() => {
    if (state.currentMod?.sha && profileState.selectedProfile?.id) {
      loadPreviewPaths(state.currentMod.sha);
    } else {
      // Clear previews when no mod is selected
      setState((prev) => ({
        ...prev,
        previewPaths: [],
        currentPreviewIndex: 0,
      }));
    }
  }, [state.currentMod?.sha, profileState.selectedProfile?.id, loadPreviewPaths]);

  const setCurrentMod = useCallback((mod: ModInfo | null) => {
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

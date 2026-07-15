import React, { createContext, useContext, useEffect, useState, ReactNode } from 'react';
import { settingsService } from '../services/ipc';
import { useSettingsStore } from '../../modules/setting/store/settingsStore';

/**
 * Annotation levels for tooltips
 * - all: show all tooltips (levels 1, 2, 3)
 * - more: show detailed tooltips (levels 1, 2)
 * - less: show only basic tooltips (level 1)
 * - off: disable all tooltips
 */
export type AnnotationLevel = 'all' | 'more' | 'less' | 'off';

/** Tooltip detail level: 1 basic (always unless "off"), 2 detailed ("more"/"all"), 3 expert ("all"). */
export type TooltipLevel = 1 | 2 | 3;

interface AnnotationContextType {
  annotationLevel: AnnotationLevel;
  setAnnotationLevel: (level: AnnotationLevel) => void;
}

const AnnotationContext = createContext<AnnotationContextType>({
  annotationLevel: 'all',
  setAnnotationLevel: () => {},
});

/** Hook to access the annotation level. Returns the default ('all') outside a provider. */
export const useAnnotation = () => useContext(AnnotationContext);

interface AnnotationProviderProps {
  children: ReactNode;
  initialLevel?: AnnotationLevel;
}

const VALID_LEVELS: AnnotationLevel[] = ['all', 'more', 'less', 'off'];

/**
 * Provider for the annotation/tooltip-verbosity level.
 *
 * Lives in shared/context (L3) — NOT in shared/components/common (the L1/L2 atom zone) — because it
 * touches services/ipc + the settings store. Mirrors ThemeProvider: it reads the level from
 * settingsStore (SettingsProvider loads global settings into the store once on startup) instead of
 * making its own on-mount GET_GLOBAL call, and persists changes via settingsService, keeping the store
 * in sync. (Previously this was AnnotationProvider in common/TooltipSystem.tsx, which did direct IPC
 * from the atom layer — the code-review layer violation this fixes.)
 */
export function AnnotationProvider({ children, initialLevel = 'all' }: AnnotationProviderProps) {
  const globalSettings = useSettingsStore((s) => s.globalSettings);

  const [annotationLevel, setLevelState] = useState<AnnotationLevel>(() => {
    const fromStore = globalSettings?.annotationLevel as AnnotationLevel | undefined;
    return fromStore && VALID_LEVELS.includes(fromStore) ? fromStore : initialLevel;
  });

  // React to the store once SettingsProvider has loaded global settings (ignore empty/invalid values).
  useEffect(() => {
    const level = globalSettings?.annotationLevel as AnnotationLevel | undefined;
    if (level && VALID_LEVELS.includes(level)) setLevelState(level);
  }, [globalSettings]);

  const setAnnotationLevel = async (level: AnnotationLevel) => {
    // Optimistic update; backend is the source of truth.
    setLevelState(level);
    try {
      await settingsService.updateGlobalSetting('annotationLevel', level);
      const { globalSettings: current, setGlobalSettings } = useSettingsStore.getState();
      if (current) setGlobalSettings({ ...current, annotationLevel: level });
    } catch {
      // Revert to the store's value on failure so UI stays in sync with the backend.
      const stored = useSettingsStore.getState().globalSettings?.annotationLevel as AnnotationLevel | undefined;
      if (stored && VALID_LEVELS.includes(stored)) setLevelState(stored);
    }
  };

  return (
    <AnnotationContext.Provider value={{ annotationLevel, setAnnotationLevel }}>
      {children}
    </AnnotationContext.Provider>
  );
}

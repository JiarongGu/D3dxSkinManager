import React, { createContext, useContext, useState, useEffect, ReactNode } from 'react';
import { settingsService } from '../services/ipc';
import { useSettingsStore } from '../../modules/setting/store/settingsStore';

export type ThemeMode = 'light' | 'dark' | 'auto';

interface ThemeContextValue {
  theme: ThemeMode;
  effectiveTheme: 'light' | 'dark';
  setTheme: (theme: ThemeMode) => void;
  isLoading: boolean;
}

const ThemeContext = createContext<ThemeContextValue | undefined>(undefined);

interface ThemeProviderProps {
  children: ReactNode;
}

/**
 * Detect system theme preference
 */
function getSystemTheme(): 'light' | 'dark' {
  if (typeof window === 'undefined') return 'light';

  const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
  return prefersDark ? 'dark' : 'light';
}

/**
 * Provider for theme management
 *
 * OPTIMIZATION: Reads theme from settingsStore instead of calling backend directly.
 * SettingsProvider is responsible for loading settings into the store on mount.
 * This prevents duplicate GET_GLOBAL calls on startup (was causing 8+ simultaneous calls).
 */
export function ThemeProvider({ children }: ThemeProviderProps) {
  const [theme, setThemeState] = useState<ThemeMode>('light');
  const [systemTheme, setSystemTheme] = useState<'light' | 'dark'>(getSystemTheme());

  // Read from settingsStore instead of calling backend
  const globalSettings = useSettingsStore((state) => state.globalSettings);
  const isLoading = useSettingsStore((state) => state.globalSettingsLoading);

  // Calculate effective theme (resolve 'auto' to actual theme)
  const effectiveTheme: 'light' | 'dark' = theme === 'auto' ? systemTheme : theme;

  // Subscribe to settings store and update theme when settings are loaded
  useEffect(() => {
    if (globalSettings?.theme) {
      setThemeState(globalSettings.theme as ThemeMode);
    }
  }, [globalSettings]);

  // Listen for system theme changes
  useEffect(() => {
    const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');

    const handleChange = (e: MediaQueryListEvent) => {
      setSystemTheme(e.matches ? 'dark' : 'light');
    };

    mediaQuery.addEventListener('change', handleChange);
    return () => mediaQuery.removeEventListener('change', handleChange);
  }, []);

  // Update backend when theme changes
  const setTheme = async (newTheme: ThemeMode) => {
    // Optimistically update UI
    setThemeState(newTheme);

    // Save to backend - this is the ONLY source of truth
    try {
      await settingsService.updateGlobalSetting('theme', newTheme);
      // Update the store to keep it in sync
      const { setGlobalSettings } = useSettingsStore.getState();
      if (globalSettings) {
        setGlobalSettings({ ...globalSettings, theme: newTheme });
      }
    } catch (error: unknown) {
      // On failure, revert to store value to stay in sync
      if (globalSettings?.theme) {
        setThemeState(globalSettings.theme as ThemeMode);
      }
    }
  };

  // Apply theme to document
  useEffect(() => {
    document.documentElement.setAttribute('data-theme', effectiveTheme);
  }, [effectiveTheme]);

  return (
    <ThemeContext.Provider value={{ theme, effectiveTheme, setTheme, isLoading }}>
      {children}
    </ThemeContext.Provider>
  );
}

/**
 * Hook to access theme context
 */
export function useTheme() {
  const context = useContext(ThemeContext);
  if (!context) {
    throw new Error('useTheme must be used within ThemeProvider');
  }
  return context;
}

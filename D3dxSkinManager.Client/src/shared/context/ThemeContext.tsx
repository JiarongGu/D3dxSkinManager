import React, { createContext, useContext, useState, useEffect, ReactNode } from 'react';
import { settingsService } from '../../modules/settings/services/settingsService';

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
 */
export function ThemeProvider({ children }: ThemeProviderProps) {
  const [theme, setThemeState] = useState<ThemeMode>('light');
  const [systemTheme, setSystemTheme] = useState<'light' | 'dark'>(getSystemTheme());
  const [isLoading, setIsLoading] = useState(true);

  // Calculate effective theme (resolve 'auto' to actual theme)
  const effectiveTheme: 'light' | 'dark' = theme === 'auto' ? systemTheme : theme;

  // Load theme from backend on mount with retry logic
  useEffect(() => {
    const loadTheme = async () => {
      const maxRetries = 3;
      const initialDelay = 500; // Start with 500ms

      for (let attempt = 0; attempt < maxRetries; attempt++) {
        try {
          const settings = await settingsService.getGlobalSettings();
          const themeValue = settings.theme as ThemeMode;
          console.log('[ThemeContext] Loaded theme from backend:', themeValue);
          setThemeState(themeValue);
          setIsLoading(false);
          return; // Success - exit retry loop
        } catch (error) {
          const isLastAttempt = attempt === maxRetries - 1;

          if (isLastAttempt) {
            console.error('[ThemeContext] Failed to load theme from backend after retries:', error);
            // Default to light theme on final failure
            setThemeState('light');
            setIsLoading(false);
          } else {
            // Wait before retry with exponential backoff
            const delay = initialDelay * Math.pow(2, attempt);
            console.log(`[ThemeContext] Retry ${attempt + 1}/${maxRetries} in ${delay}ms...`);
            await new Promise(resolve => setTimeout(resolve, delay));
          }
        }
      }
    };

    loadTheme();
  }, []);

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
    } catch (error) {
      console.error('[ThemeContext] Failed to save theme to backend:', error);
      // On failure, reload from backend to stay in sync
      try {
        const settings = await settingsService.getGlobalSettings();
        setThemeState(settings.theme as ThemeMode);
      } catch {
        // If we can't reload, keep the optimistic update
        // User will see the change but it won't persist on reload
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

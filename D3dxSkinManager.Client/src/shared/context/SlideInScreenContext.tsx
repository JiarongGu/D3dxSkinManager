import React, { createContext, useContext, useState, useCallback, ReactNode } from 'react';
import { v4 as uuidv4 } from 'uuid';

export interface SlideInScreenConfig {
  id: string;
  title: string;
  content: ReactNode;
  width?: string;
  onClose?: () => void;
  isClosing?: boolean;
  level: number;
  parentId?: string;
  loading?: boolean;
  loadingText?: string;
  /** No header bar — the content owns the full panel; a floating close button is rendered instead.
   * For screens that carry their own title (e.g. the remote mod detail's info card). */
  headless?: boolean;
}

interface SlideInScreenContextValue {
  screens: SlideInScreenConfig[];
  openScreen: (config: Omit<SlideInScreenConfig, 'id' | 'level' | 'parentId'>, parentId?: string) => string;
  closeScreen: (id: string) => void;
  closeTopScreen: () => void;
  closeAllScreens: () => void;
  getCurrentLevel: () => number;
  setLoading: (id: string, loading: boolean, loadingText?: string) => void;
  currentScreenId?: string;
}

const SlideInScreenContext = createContext<SlideInScreenContextValue | undefined>(undefined);

interface SlideInScreenProviderProps {
  children: ReactNode;
}

/**
 * Provider for managing slide-in screen stack
 */
export function SlideInScreenProvider({ children }: SlideInScreenProviderProps) {
  const [screens, setScreens] = useState<SlideInScreenConfig[]>([]);
  const [currentScreenId, setCurrentScreenId] = useState<string | undefined>(undefined);

  const getCurrentLevel = useCallback(() => {
    if (!currentScreenId) return 0;
    const currentScreen = screens.find(s => s.id === currentScreenId);
    return currentScreen?.level ?? 0;
  }, [screens, currentScreenId]);

  const openScreen = useCallback((config: Omit<SlideInScreenConfig, 'id' | 'level' | 'parentId'>, parentId?: string) => {
    const id = uuidv4();

    // Calculate level based on parent
    let level = 1;
    if (parentId) {
      const parent = screens.find(s => s.id === parentId);
      level = parent ? parent.level + 1 : 1;
    }

    // Close other screens at the same level (siblings)
    const screensToClose = screens.filter(s => s.level === level && !s.isClosing);

    // Use a single state update to mark all screens as closing
    if (screensToClose.length > 0) {
      setScreens(prev =>
        prev.map(screen =>
          screensToClose.some(s => s.id === screen.id)
            ? { ...screen, isClosing: true }
            : screen
        )
      );

      // Remove screens after animation and call their onClose callbacks
      setTimeout(() => {
        setScreens(prev => {
          const removedScreens = prev.filter(screen =>
            screensToClose.some(s => s.id === screen.id)
          );

          // Call onClose for each removed screen
          removedScreens.forEach(screen => {
            if (screen.onClose) {
              setTimeout(() => screen.onClose?.(), 0);
            }
          });

          return prev.filter(screen => !screensToClose.some(s => s.id === screen.id));
        });
      }, 200); // Match animation duration
    }

    const newScreen: SlideInScreenConfig = {
      ...config,
      id,
      level,
      parentId,
    };

    setScreens(prev => [...prev, newScreen]);
    return id;
  }, [screens]);

  const closeScreen = useCallback((id: string) => {
    // Mark screen as closing, which triggers animation
    setScreens(prev =>
      prev.map(s => (s.id === id ? { ...s, isClosing: true } : s))
    );

    // Remove screen and call onClose after animation completes
    setTimeout(() => {
      setScreens(prev => {
        const screen = prev.find(s => s.id === id);
        // Call onClose after screen is removed from DOM
        if (screen?.onClose) {
          setTimeout(() => screen.onClose?.(), 0);
        }
        return prev.filter(s => s.id !== id);
      });
    }, 200); // Match animation duration
  }, []);

  const closeTopScreen = useCallback(() => {
    setScreens(prev => {
      if (prev.length === 0) return prev;
      const topScreen = prev[prev.length - 1];
      if (topScreen.onClose) {
        topScreen.onClose();
      }
      return prev.slice(0, -1);
    });
  }, []);

  const closeAllScreens = useCallback(() => {
    setScreens(prev => {
      prev.forEach(screen => {
        if (screen.onClose) {
          screen.onClose();
        }
      });
      return [];
    });
  }, []);

  const setLoading = useCallback((id: string, loading: boolean, loadingText?: string) => {
    setScreens(prev =>
      prev.map(s => (s.id === id ? { ...s, loading, loadingText } : s))
    );
  }, []);

  return (
    <SlideInScreenContext.Provider
      value={{
        screens,
        openScreen,
        closeScreen,
        closeTopScreen,
        closeAllScreens,
        getCurrentLevel,
        setLoading,
        currentScreenId,
      }}
    >
      {children}
    </SlideInScreenContext.Provider>
  );
}

/**
 * Hook to access slide-in screen context
 */
export function useSlideInScreenContext() {
  const context = useContext(SlideInScreenContext);
  if (!context) {
    throw new Error('useSlideInScreen must be used within a SlideInScreenProvider');
  }
  return context;
}

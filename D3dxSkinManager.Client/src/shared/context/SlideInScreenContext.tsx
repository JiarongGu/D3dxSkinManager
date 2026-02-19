import React, { createContext, useContext, useState, useCallback, ReactNode } from 'react';

export interface SlideInScreenConfig {
  id: string;
  title: string;
  content: ReactNode;
  width?: string;
  onClose?: () => void;
  isClosing?: boolean;
}

interface SlideInScreenContextValue {
  screens: SlideInScreenConfig[];
  openScreen: (config: Omit<SlideInScreenConfig, 'id'>) => string;
  closeScreen: (id: string) => void;
  closeTopScreen: () => void;
  closeAllScreens: () => void;
}

const SlideInScreenContext = createContext<SlideInScreenContextValue | undefined>(undefined);

interface SlideInScreenProviderProps {
  children: ReactNode;
}

let screenIdCounter = 0;

/**
 * Provider for managing slide-in screen stack
 */
export function SlideInScreenProvider({ children }: SlideInScreenProviderProps) {
  const [screens, setScreens] = useState<SlideInScreenConfig[]>([]);

  const openScreen = useCallback((config: Omit<SlideInScreenConfig, 'id'>) => {
    const id = `screen_${++screenIdCounter}_${Date.now()}`;
    const newScreen: SlideInScreenConfig = {
      ...config,
      id,
    };

    setScreens(prev => [...prev, newScreen]);
    return id;
  }, []);

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

  return (
    <SlideInScreenContext.Provider
      value={{
        screens,
        openScreen,
        closeScreen,
        closeTopScreen,
        closeAllScreens,
      }}
    >
      {children}
    </SlideInScreenContext.Provider>
  );
}

/**
 * Hook to access slide-in screen context
 */
export function useSlideInScreen() {
  const context = useContext(SlideInScreenContext);
  if (!context) {
    throw new Error('useSlideInScreen must be used within a SlideInScreenProvider');
  }
  return context;
}

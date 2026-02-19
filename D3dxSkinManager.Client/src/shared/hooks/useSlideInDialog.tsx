import { useEffect, useRef } from 'react';
import { useSlideInScreen } from '../context/SlideInScreenContext';

interface SlideInDialogOptions {
  visible: boolean;
  title: string;
  content: React.ReactNode;
  width?: string;
  onClose?: () => void;
}

/**
 * Hook to manage dialog-to-slideIn screen migration
 * Provides a bridge between old visible prop pattern and new slide-in system
 */
export function useSlideInDialog({
  visible,
  title,
  content,
  width = '60%',
  onClose,
}: SlideInDialogOptions) {
  const { openScreen, closeScreen } = useSlideInScreen();
  const screenIdRef = useRef<string | null>(null);

  useEffect(() => {
    if (visible && !screenIdRef.current) {
      // Open screen
      screenIdRef.current = openScreen({
        title,
        width,
        content,
        onClose,
      });
    } else if (!visible && screenIdRef.current) {
      // Close screen
      closeScreen(screenIdRef.current);
      screenIdRef.current = null;
    }
  }, [visible, title, content, width, openScreen, closeScreen, onClose]);

  // Cleanup on unmount
  useEffect(() => {
    return () => {
      if (screenIdRef.current) {
        closeScreen(screenIdRef.current);
      }
    };
  }, [closeScreen]);
}

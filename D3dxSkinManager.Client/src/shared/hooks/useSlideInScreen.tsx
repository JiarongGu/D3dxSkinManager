import { useEffect, useRef } from 'react';
import { useSlideInScreenContext } from '../context/SlideInScreenContext';
import { useCurrentSlideInScreenId } from '../components/common/SlideInScreen';

interface SlideInDialogOptions {
  visible: boolean;
  title: string;
  content: React.ReactNode;
  width?: string;
  onClose?: () => void;
  /** Extra class on the slide-in container — lets a screen scope its own geometry (narrow panel). */
  className?: string;
}

/**
 * Hook to manage dialog-to-slideIn screen migration
 * Provides a bridge between old visible prop pattern and new slide-in system
 *
 * Level tracking:
 * - Level is automatically determined based on current screen stack depth
 * - Child screens (screens opened from within other screens) get higher levels
 * - Same-level screens automatically close when a new one opens
 *
 * Returns:
 * - screenId: The ID of the current screen (or undefined if not visible)
 * - setLoading: Function to set loading state for the screen (automatically manages closable state)
 */
export function useSlideInScreen({
  visible,
  title,
  content,
  width = '60%',
  onClose,
  className,
}: SlideInDialogOptions) {
  const { openScreen, closeScreen, setLoading: setLoadingContext } = useSlideInScreenContext();
  const parentScreenId = useCurrentSlideInScreenId();
  const screenIdRef = useRef<string>(undefined);

  useEffect(() => {
    if (visible && !screenIdRef.current) {
      // Open screen with parent ID (if called from within another screen)
      screenIdRef.current = openScreen({
        title,
        width,
        content,
        onClose,
        className,
      }, parentScreenId);
    } else if (!visible && screenIdRef.current) {
      // Close screen
      closeScreen(screenIdRef.current);
      screenIdRef.current = undefined;
    }
  }, [visible, title, content, width, className, openScreen, closeScreen, onClose, parentScreenId]);

  // Cleanup on unmount
  useEffect(() => {
    return () => {
      if (screenIdRef.current) {
        closeScreen(screenIdRef.current);
      }
    };
  }, [closeScreen]);

  // Return setLoading function that works with current screen ID
  const setLoading = (loading: boolean, loadingText?: string) => {
    if (screenIdRef.current) {
      setLoadingContext(screenIdRef.current, loading, loadingText);
    }
  };

  return {
    screenId: screenIdRef.current,
    setLoading,
  };
}

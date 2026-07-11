import React, { createContext, useContext, useState, useEffect } from "react";
import { settingsService } from "../../services/ipc";
/**
 * Annotation levels for tooltips
 * - all: Show all tooltips (levels 1, 2, 3)
 * - more: Show detailed tooltips (levels 1, 2)
 * - less: Show only basic tooltips (level 1)
 * - off: Disable all tooltips
 */
export type AnnotationLevel = "all" | "more" | "less" | "off";

/**
 * Tooltip detail level
 * - 1: Basic (always show unless "off")
 * - 2: Detailed (show in "more" and "all")
 * - 3: Expert (show only in "all")
 */
export type TooltipLevel = 1 | 2 | 3;

interface AnnotationContextType {
  annotationLevel: AnnotationLevel;
  setAnnotationLevel: (level: AnnotationLevel) => void;
}

const AnnotationContext = createContext<AnnotationContextType>({
  annotationLevel: "all",
  setAnnotationLevel: () => {},
});

/**
 * Hook to access annotation level settings
 */
export const useAnnotation = () => useContext(AnnotationContext);

interface AnnotationProviderProps {
  children: React.ReactNode;
  initialLevel?: AnnotationLevel;
}

/**
 * Provider component for annotation level management
 * Wrap your app with this to enable annotation system
 */
export const AnnotationProvider: React.FC<AnnotationProviderProps> = ({
  children,
  initialLevel = "all",
}) => {
  const [annotationLevel, setAnnotationLevel] =
    useState<AnnotationLevel>(initialLevel);

  // Load annotation level from backend on mount with retry logic
  useEffect(() => {
    const loadAnnotationLevel = async () => {
      const maxRetries = 3;
      const initialDelay = 500; // Start with 500ms

      for (let attempt = 0; attempt < maxRetries; attempt++) {
        try {
          const settings = await settingsService.getGlobalSettings();
          const level = settings.annotationLevel as AnnotationLevel;
          if (level && ["all", "more", "less", "off"].includes(level)) {
            setAnnotationLevel(level);
            return; // Success - exit retry loop
          }
        } catch (error: unknown) {
          const isLastAttempt = attempt === maxRetries - 1;

          if (isLastAttempt) {
                        // Default to 'all' on final failure
            setAnnotationLevel("all");
          } else {
            // Wait before retry with exponential backoff
            const delay = initialDelay * Math.pow(2, attempt);
                        await new Promise((resolve) => setTimeout(resolve, delay));
          }
        }
      }
    };

    loadAnnotationLevel();
  }, []);

  // Save annotation level to backend when changed
  const handleSetAnnotationLevel = async (level: AnnotationLevel) => {
    // Optimistically update UI
    setAnnotationLevel(level);

    // Save to backend - this is the ONLY source of truth
    try {
      await settingsService.updateGlobalSetting("annotationLevel", level);
    } catch (error: unknown) {
            // On failure, reload from backend to stay in sync
      try {
        const settings = await settingsService.getGlobalSettings();
        setAnnotationLevel(settings.annotationLevel as AnnotationLevel);
      } catch {
        // If we can't reload, keep the optimistic update
      }
    }
  };

  return (
    <AnnotationContext.Provider
      value={{
        annotationLevel,
        setAnnotationLevel: handleSetAnnotationLevel,
      }}
    >
      {children}
    </AnnotationContext.Provider>
  );
};


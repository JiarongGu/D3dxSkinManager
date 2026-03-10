import React, { createContext, useContext, useState, useEffect } from "react";
import { Tooltip } from "antd";
import type { TooltipPlacement } from "antd/es/tooltip";
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

export interface AnnotatedTooltipProps {
  title: React.ReactNode;
  level?: TooltipLevel;
  placement?: TooltipPlacement;
  children: React.ReactElement;
  mouseEnterDelay?: number;
  overlayStyle?: React.CSSProperties;
}

/**
 * Enhanced tooltip component with annotation level support
 * Automatically shows/hides based on current annotation level
 */
export const AnnotatedTooltip: React.FC<AnnotatedTooltipProps> = ({
  title,
  level = 1,
  placement = "top",
  children,
  mouseEnterDelay = 0.5,
  overlayStyle,
}) => {
  const { annotationLevel } = useAnnotation();

  // Determine if tooltip should be visible based on level
  const shouldShow = (): boolean => {
    if (annotationLevel === "off") return false;
    if (annotationLevel === "less") return level === 1;
    if (annotationLevel === "more") return level === 1 || level === 2;
    if (annotationLevel === "all") return true;
    return false;
  };

  // If tooltip shouldn't show, return children without wrapper
  if (!shouldShow() || !title) {
    return children;
  }

  return (
    <Tooltip
      title={title}
      placement={placement}
      mouseEnterDelay={mouseEnterDelay}
      styles={{
        root: {
          maxWidth: "400px",
          ...overlayStyle,
        },
      }}
    >
      {children}
    </Tooltip>
  );
};

/**
 * Annotation content builder for common UI elements
 */
export const annotations = {
  // Mod Management
  modTable: {
    loadButton: {
      level: 1 as TooltipLevel,
      title:
        "Load this mod into the game. Loaded mods will be active when you start the game.",
    },
    unloadButton: {
      level: 1 as TooltipLevel,
      title:
        "Unload this mod from the game. The mod will remain in your library.",
    },
    deleteButton: {
      level: 2 as TooltipLevel,
      title:
        "Permanently delete this mod from your library. This action cannot be undone.",
    },
    editButton: {
      level: 1 as TooltipLevel,
      title: "Edit mod info (name, description, author, tags, grading)",
    },
    shaColumn: {
      level: 3 as TooltipLevel,
      title: "Mod ID - Unique identifier for this mod",
    },
    gradingColumn: {
      level: 2 as TooltipLevel,
      title: "Your rating of this mod (0-5 stars)",
    },
  },

  // Search & Filters
  search: {
    modSearch: {
      level: 1 as TooltipLevel,
      title:
        'Search mods by name, author, or tags. Use "!" prefix to exclude terms (e.g., "!NSFW")',
    },
    categorySearch: {
      level: 1 as TooltipLevel,
      title: "Filter Categories by name",
    },
  },

  // Import Window
  importWindow: {
    taskId: {
      level: 2 as TooltipLevel,
      title: "Unique task identifier. Tasks are processed in order.",
    },
    editTask: {
      level: 1 as TooltipLevel,
      title: "Edit import properties for this task",
    },
    removeTask: {
      level: 1 as TooltipLevel,
      title: "Remove this task from the import queue",
    },
    batchEdit: {
      level: 2 as TooltipLevel,
      title: "Edit common properties for all selected tasks",
    },
    confirmImport: {
      level: 1 as TooltipLevel,
      title:
        "Start importing all pending tasks. Requires Name and Category for each task.",
    },
  },

  // Dialogs
  modEdit: {
    name: {
      level: 1 as TooltipLevel,
      title: "Display name for this mod",
    },
    category: {
      level: 2 as TooltipLevel,
      title: 'Category or character name (e.g., "Character", "Weapon", "UI")',
    },
    description: {
      level: 1 as TooltipLevel,
      title: "Description of what this mod changes (max 500 characters)",
    },
    author: {
      level: 1 as TooltipLevel,
      title: "Mod creator's name",
    },
    grading: {
      level: 2 as TooltipLevel,
      title: "Rate this mod's quality (0-5 stars)",
    },
    tags: {
      level: 2 as TooltipLevel,
      title: 'Categorize this mod with tags (e.g., "HD", "NSFW", "Recolor")',
    },
    sha: {
      level: 3 as TooltipLevel,
      title: "Mod ID - Cannot be edited (read-only)",
    },
  },

  // Settings
  settings: {
    annotationLevel: {
      level: 1 as TooltipLevel,
      title:
        "Control tooltip detail level: All (show everything), More (detailed), Less (basic only), Off (disabled)",
    },
    theme: {
      level: 1 as TooltipLevel,
      title: "Choose application color theme",
    },
    language: {
      level: 1 as TooltipLevel,
      title: "Select interface language",
    },
  },

  // Context Menu
  contextMenu: {
    loadMod: {
      level: 1 as TooltipLevel,
      title: "Load this mod into the game",
    },
    unloadMod: {
      level: 1 as TooltipLevel,
      title: "Unload this mod from the game",
    },
    copyModName: {
      level: 2 as TooltipLevel,
      title: "Copy mod name to clipboard",
    },
    copySha: {
      level: 3 as TooltipLevel,
      title: "Copy Mod ID to clipboard",
    },
    viewFiles: {
      level: 2 as TooltipLevel,
      title: "Open mod files in file explorer",
    },
    exportMod: {
      level: 2 as TooltipLevel,
      title: "Export this mod as a ZIP file",
    },
  },

  // Status Bar
  statusBar: {
    helpButton: {
      level: 1 as TooltipLevel,
      title: "Open help documentation",
    },
    modsCount: {
      level: 1 as TooltipLevel,
      title: "Number of loaded mods / Total mods",
    },
  },
};

/**
 * Helper function to get annotation level label
 */
export const getAnnotationLevelLabel = (level: AnnotationLevel): string => {
  switch (level) {
    case "all":
      return "All (ȫ��)";
    case "more":
      return "More (�϶�)";
    case "less":
      return "Less (����)";
    case "off":
      return "Off (�ر�)";
    default:
      return "All";
  }
};

/**
 * Helper function to get annotation level description
 */
export const getAnnotationLevelDescription = (
  level: AnnotationLevel,
): string => {
  switch (level) {
    case "all":
      return "Show all tooltips including expert-level details";
    case "more":
      return "Show detailed tooltips for most features";
    case "less":
      return "Show only basic tooltips";
    case "off":
      return "Disable all tooltips";
    default:
      return "";
  }
};

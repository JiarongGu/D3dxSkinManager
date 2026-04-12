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
      titleKey: "tooltip.modTable.loadButton",
    },
    unloadButton: {
      level: 1 as TooltipLevel,
      titleKey: "tooltip.modTable.unloadButton",
    },
    deleteButton: {
      level: 2 as TooltipLevel,
      titleKey: "tooltip.modTable.deleteButton",
    },
    editButton: {
      level: 1 as TooltipLevel,
      titleKey: "tooltip.modTable.editButton",
    },
    shaColumn: {
      level: 3 as TooltipLevel,
      titleKey: "tooltip.modTable.shaColumn",
    },
    gradingColumn: {
      level: 2 as TooltipLevel,
      titleKey: "tooltip.modTable.gradingColumn",
    },
  },

  // Search & Filters
  search: {
    modSearch: {
      level: 1 as TooltipLevel,
      titleKey: "tooltip.search.modSearch",
    },
    categorySearch: {
      level: 1 as TooltipLevel,
      titleKey: "tooltip.search.categorySearch",
    },
  },

  // Import Window
  importWindow: {
    taskId: {
      level: 2 as TooltipLevel,
      titleKey: "tooltip.importWindow.taskId",
    },
    editTask: {
      level: 1 as TooltipLevel,
      titleKey: "tooltip.importWindow.editTask",
    },
    removeTask: {
      level: 1 as TooltipLevel,
      titleKey: "tooltip.importWindow.removeTask",
    },
    batchEdit: {
      level: 2 as TooltipLevel,
      titleKey: "tooltip.importWindow.batchEdit",
    },
    confirmImport: {
      level: 1 as TooltipLevel,
      titleKey: "tooltip.importWindow.confirmImport",
    },
  },

  // Dialogs
  modEdit: {
    name: {
      level: 1 as TooltipLevel,
      titleKey: "tooltip.modEdit.name",
    },
    category: {
      level: 2 as TooltipLevel,
      titleKey: "tooltip.modEdit.category",
    },
    description: {
      level: 1 as TooltipLevel,
      titleKey: "tooltip.modEdit.description",
    },
    author: {
      level: 1 as TooltipLevel,
      titleKey: "tooltip.modEdit.author",
    },
    grading: {
      level: 2 as TooltipLevel,
      titleKey: "tooltip.modEdit.grading",
    },
    tags: {
      level: 2 as TooltipLevel,
      titleKey: "tooltip.modEdit.tags",
    },
    id: {
      level: 3 as TooltipLevel,
      titleKey: "tooltip.modEdit.id",
    },
  },

  // Settings
  settings: {
    annotationLevel: {
      level: 1 as TooltipLevel,
      titleKey: "tooltip.settings.annotationLevel",
    },
    theme: {
      level: 1 as TooltipLevel,
      titleKey: "tooltip.settings.theme",
    },
    language: {
      level: 1 as TooltipLevel,
      titleKey: "tooltip.settings.language",
    },
  },

  // Context Menu
  contextMenu: {
    loadMod: {
      level: 1 as TooltipLevel,
      titleKey: "tooltip.contextMenu.loadMod",
    },
    unloadMod: {
      level: 1 as TooltipLevel,
      titleKey: "tooltip.contextMenu.unloadMod",
    },
    copyModName: {
      level: 2 as TooltipLevel,
      titleKey: "tooltip.contextMenu.copyModName",
    },
    copySha: {
      level: 3 as TooltipLevel,
      titleKey: "tooltip.contextMenu.copySha",
    },
    viewFiles: {
      level: 2 as TooltipLevel,
      titleKey: "tooltip.contextMenu.viewFiles",
    },
    exportMod: {
      level: 2 as TooltipLevel,
      titleKey: "tooltip.contextMenu.exportMod",
    },
  },

  // Status Bar
  statusBar: {
    helpButton: {
      level: 1 as TooltipLevel,
      titleKey: "statusBar.help",
    },
    modsCount: {
      level: 1 as TooltipLevel,
      titleKey: "tooltip.statusBar.modsCount",
    },
  },
};

/**
 * Translation keys for annotation level labels
 */
export const annotationLevelLabelKeys: Record<AnnotationLevel, string> = {
  all: "settings.global.annotationLevel.all",
  more: "settings.global.annotationLevel.more",
  less: "settings.global.annotationLevel.less",
  off: "settings.global.annotationLevel.off",
};

/**
 * Translation keys for annotation level descriptions
 */
export const annotationLevelDescKeys: Record<AnnotationLevel, string> = {
  all: "settings.global.annotationLevel.allDesc",
  more: "settings.global.annotationLevel.moreDesc",
  less: "settings.global.annotationLevel.lessDesc",
  off: "settings.global.annotationLevel.offDesc",
};

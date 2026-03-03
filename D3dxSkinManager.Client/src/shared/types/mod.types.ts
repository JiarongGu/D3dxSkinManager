export interface ModInfo {
  sha: string;
  category: string;
  categoryName?: string;  // Human-readable category name for display
  name: string;
  author: string;
  description: string;
  type: string;
  grading: string;
  tags: string[];
  tagsWithMetadata?: Tag[];  // Tag objects with colors (populated by backend)
  disablePreview: boolean;  // If true, preview images won't be loaded/displayed for this mod
  isLoaded: boolean;
  isAvailable: boolean;
  hasCache: boolean;  // True if cache directory exists (either active or DISABLED-)
  hasPreviewFolder: boolean;  // True if preview directory exists with preview images
  // Note: Preview images and thumbnails are stored dynamically in previews/{SHA}/ folder
  // Use modService.getPreviewPaths(sha) to fetch them
  // The first preview image (sorted alphabetically) is used as the thumbnail
  // File paths (for viewing operations - populated on-demand, not stored in DB)
  originalPath?: string;  // Path to original archive file
  cachePath?: string;     // Path to cache directory (extracted files in active/loaded state)
  // Note: workPath is deprecated - use cachePath instead. Cache folder can be in loaded or unloaded/disabled mode
}

export type GradingLevel = 'G' | 'P' | 'R' | 'X';

/**
 * Request model for updating mod metadata
 * Optional fields allow partial updates - only provided values will be applied
 */
export interface UpdateModMetadataRequest {
  name?: string;
  author?: string;
  tags?: string[];
  grading?: string;
  description?: string;
  disablePreview?: boolean;
}

export interface ModFilters {
  searchTerm: string;
  selectedObject: string;
  selectedGrading: string;
}

export interface ModStatistics {
  totalMods: number;
  loadedMods: number;
  availableMods: number;
}

/**
 * Result of mod load operation with affected mods for efficient updates
 * Avoids full mod list refresh by returning only what changed
 */
export interface ModLoadResult {
  /** SHA of the mod that was loaded */
  loadedModSha: string;
  /** SHAs of mods that were automatically unloaded (same category conflicts) */
  unloadedModShas: string[];
  /** Whether the load operation succeeded */
  success: boolean;
}

/**
 * Tag definition from Tags table (master list)
 * Tags are defined here with colors, then referenced by name in mod.tags
 */
export interface Tag {
  /** Tag name (unique identifier) */
  name: string;
  /** Tag color in hex format (e.g., "#1890ff") */
  color: string;
  /** When this tag was created */
  createdAt: string;
  /** When this tag was last updated */
  updatedAt: string;
}

/**
 * Mod keybinding information parsed from .ini files
 */
export interface ModKeybinding {
  /** Section name from .ini file (e.g., "KeyBodyColor", "KeyHorn") */
  sectionName: string;
  /** The key assigned (e.g., "9", "i", "VK_UP", "[") */
  key: string;
  /** Display name for the key (converted from technical names like VK_UP to friendly names) */
  keyDisplay: string;
  /** Description/purpose extracted from section name (e.g., "Body Color", "Horn") */
  description: string;
  /** Keybinding type (e.g., "cycle", "toggle", "hold") */
  type: string;
  /** Associated variable name (e.g., "$color", "$horn") */
  variable: string;
  /** Values for cycle type (e.g., "0,1,2,3") */
  cycleValues: string;
}

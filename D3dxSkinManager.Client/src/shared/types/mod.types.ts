export interface ModInfo {
  sha: string;
  category: string;
  name: string;
  author: string;
  description: string;
  type: string;
  grading: string;
  tags: string[];
  isLoaded: boolean;
  isAvailable: boolean;
  // Note: Preview images and thumbnails are stored dynamically in previews/{SHA}/ folder
  // Use modService.getPreviewPaths(sha) to fetch them
  // The first preview image (sorted alphabetically) is used as the thumbnail
  // File paths (for viewing operations - populated on-demand, not stored in DB)
  originalPath?: string;  // Path to original archive file
  workPath?: string;      // Path to extracted/working directory
  cachePath?: string;     // Path to cache directory (for disabled mods)
}

export type GradingLevel = 'G' | 'P' | 'R' | 'X';

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

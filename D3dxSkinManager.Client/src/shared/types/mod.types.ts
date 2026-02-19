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
  thumbnailPath?: string;
  previewPath?: string;
  // File paths (for viewing operations)
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

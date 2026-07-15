/**
 * Profile settings bundle (.zip) export/import — matches backend ProfileBundleModels.cs.
 * A bundle carries a PORTABLE slice of a profile (metadata + config + category tree + remote libraries);
 * it excludes mod archives/DB/previews + login credentials. Import always creates a NEW profile.
 */

export interface ProfileBundleExportOptions {
  profileId: string;
  /** Folder to write the {name}.zip into. */
  outputPath: string;
  includeCategories?: boolean;
  includeRemote?: boolean;
}

export interface ProfileBundleExportResult {
  success: boolean;
  /** Absolute path of the written .zip. */
  outputPath: string;
  profileName: string;
  categoryCount: number;
  libraryCount: number;
  totalSizeBytes: number;
  errors: string[];
}

/** Read-only preview of a bundle (folder OR .zip) for the import UI. */
export interface ProfileBundleAnalysis {
  isValid: boolean;
  errorMessage?: string;
  version: string;
  profileName: string;
  description?: string;
  color?: string;
  gameName?: string;
  exportDate: string;
  hasThumbnail: boolean;
  categoryCount: number;
  libraryCount: number;
  tagLabelSourceCount: number;
  sourceOverlayCount: number;
}

export interface ProfileBundleImportOptions {
  /** A folder OR a .zip. */
  bundlePath: string;
  /** Optional name override for the new profile (defaults to the bundle's profile name). */
  newProfileName?: string;
  importCategories?: boolean;
  importRemote?: boolean;
}

export interface ProfileBundleImportResult {
  success: boolean;
  newProfileId: string;
  profileName: string;
  importedCategoryCount: number;
  importedLibraryCount: number;
  importedTagLabelCount: number;
  importedSourceOverlayCount: number;
  errors: string[];
}

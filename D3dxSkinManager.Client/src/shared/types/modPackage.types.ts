/**
 * Types for Mod Package export/import feature
 */

export interface PackageManifest {
  version: string;
  appName: string;
  exportDate: string;
  name: string;
  description: string;
  modCount: number;
  categoryCount: number;
  includesArchives: boolean;
  includesPreviews: boolean;
  categories: PackageCategory[];
  mods: PackageModEntry[];
}

export interface PackageCategory {
  id: string;
  name: string;
  parentId?: string;
  priority: number;
  description?: string;
}

export interface PackageModEntry {
  id: string;
  fileName: string;
  previewFolder?: string;
  name: string;
  author: string;
  description: string;
  categoryId: string;
  categoryPath: string;
  tags: string[];
  grading: string;
  type: string;
  hasArchive: boolean;
  hasPreviews: boolean;
}

export interface ExportConfig {
  packageName: string;
  packageDescription: string;
  outputPath: string;
  modIds: string[];
  includeArchives: boolean;
  includePreviews: boolean;
}

export interface ExportResult {
  success: boolean;
  exportedCount: number;
  outputPath: string;
  totalSizeBytes: number;
  errors: string[];
}

export interface PackageAnalysis {
  isValid: boolean;
  errorMessage?: string;
  packageName: string;
  packageDescription: string;
  exportDate: string;
  totalModCount: number;
  hasArchives: boolean;
  hasPreviews: boolean;
  categories: PackageCategory[];
  mods: AnalyzedModEntry[];
}

export interface AnalyzedModEntry {
  id: string;
  name: string;
  author: string;
  description: string;
  categoryPath: string;
  tags: string[];
  grading: string;
  hasArchive: boolean;
  hasPreviews: boolean;
  /** "new" | "update" */
  status: 'new' | 'update';
  changedFields: string[];
  localName?: string;
  localAuthor?: string;
  previewPaths: string[];
}

export interface ImportConfig {
  packagePath: string;
  selectedModIds: string[];
  updateExisting: boolean;
  importPreviews: boolean;
  createMissingCategories: boolean;
}

export interface ImportResult {
  importedCount: number;
  updatedCount: number;
  skippedCount: number;
  failedCount: number;
  errors: string[];
  importedModNames: string[];
  updatedModNames: string[];
}

export interface PackageProgress {
  operation: 'export' | 'import';
  current: number;
  total: number;
  currentModName: string;
  stage: string;
}

/**
 * Types for the mod-fix (hash-fix script runner) tool. Mirrors the backend Tool/Models/ModFixModels.cs.
 * Game-agnostic: a "fix" is any user-supplied .py/.exe/.bat/.cmd that rewrites a mod's content
 * (typically 3DMigoto .ini hashes) so it keeps working after a game update.
 */

/** A registered fix tool in the per-profile library (mirrors backend Tool/Models/ModFixModels.ModFixTool). */
export interface ModFixTool {
  id: string;
  name: string;
  /** Runnable entry relative to the tool folder. */
  entryFile: string;
  description?: string;
  recompressDefault: boolean;
  addedAt: string;
  /** Candidate runnable files (relative) to choose the single entry from when it's unresolved. */
  candidates: string[];
  /** Absolute path to the resolved entry, or undefined when unresolved (user must pick a candidate). */
  entryPath?: string;
}

export interface ModFixRequest {
  scriptPath: string;
  /** Empty/omitted = run against ALL mods. */
  modIds?: string[];
  /** Re-compress the fixed content back into the mod archive (default true). */
  recompress?: boolean;
}

/** Live progress event (MOD_FIX_PROGRESS) — one per mod as the run advances. */
export interface ModFixProgress {
  current: number;
  total: number;
  modId: string;
  modName: string;
}

/** Final result event (MOD_FIX_COMPLETE). */
export interface ModFixResult {
  total: number;
  succeeded: number;
  failed: number;
  skipped: number;
  cancelled: boolean;
  results: ModFixItemResult[];
}

export interface ModFixItemResult {
  modId: string;
  modName: string;
  success: boolean;
  skipped: boolean;
  exitCode?: number;
  output?: string;
  error?: string;
}

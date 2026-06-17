/**
 * Types for the mod-fix (hash-fix script runner) tool. Mirrors the backend Tool/Models/ModFixModels.cs.
 * Game-agnostic: a "fix" is any user-supplied .py/.exe/.bat/.cmd that rewrites a mod's content
 * (typically 3DMigoto .ini hashes) so it keeps working after a game update.
 */

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

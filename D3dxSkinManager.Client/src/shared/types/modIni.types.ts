/**
 * General mod .ini editor model — mirrors C# Modules/Mod/Models/ModIniModels.cs.
 * Backend classifies each entry: editable (Key/Constants tuning) vs advanced/read-only
 * (hash / *Override / Resource / Shader / command-list). The frontend only lets the user
 * change `value` of editable entries; the backend re-validates on write.
 */

export interface ModIniEntry {
  /** Left-hand side, trimmed (e.g. "key", "type", "$swapvar", "global persist $x"). */
  key: string;
  /** Right-hand side value, inline comment stripped. */
  value: string;
  /** 0-based line index within the file — the write-back key. */
  lineIndex: number;
  /** True when the user may safely change `value`. */
  editable: boolean;
  /** Why a locked entry is read-only ("advancedSection" | "command"). Undefined when editable. */
  lockReason?: string;
}

export interface ModIniSection {
  /** Section name without brackets (e.g. "KeySwap0", "Constants", "TextureOverrideBody"). */
  name: string;
  /** True if the whole section is advanced/read-only. */
  advanced: boolean;
  entries: ModIniEntry[];
}

export interface ModIniFile {
  /** Path relative to the mod cache dir, forward-slashed (the archive entry path too). */
  relativePath: string;
  /** File name for display (e.g. "mod.ini"). */
  fileName: string;
  /** The file's 3DMigoto `namespace` directive (e.g. "Merge\\Master"), if declared. Relates files together. */
  namespace?: string;
  sections: ModIniSection[];
}

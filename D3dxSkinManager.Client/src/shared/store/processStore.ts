/**
 * Global process store — mirror of the backend ProcessRegistry (the authoritative source of all
 * long-running operations). Fed by the SYSTEM/PROCESS_LIST_UPDATED event snapshot via
 * initProcessBridge(). The status bar + Activity panel read from here.
 *
 * Supersedes the old ephemeral taskStore (which held only frontend-registered {id,label,progress}).
 */
import { create } from 'zustand';
import type { TFunction } from 'i18next';

// NOTE: enums are camelCase because IpcHandler serializes C# enums with
// JsonStringEnumConverter(CamelCase). See .claude/rules/enum-serialization.md.
export type ProcessStatus = 'queued' | 'running' | 'completed' | 'failed' | 'cancelled' | 'interrupted';

export type ProcessType =
  | 'modLoad'
  | 'modImport'
  | 'modDelete'
  | 'presetApply'
  | 'batchUpdate'
  | 'analysis'
  | 'package'
  | 'migration'
  | 'cleanup'
  | 'archiveUpdate'
  | 'fileScan'
  | 'download'
  | 'modFix'
  | 'optimize'
  | 'other';

export interface ProcessInfo {
  id: string;
  type: ProcessType;
  status: ProcessStatus;
  /** English fallback title — prefer titleKey via processTitle() so the UI language wins. */
  title: string;
  /** i18n key for the title (interpolates titleArg as {{arg}}); title is the fallback. */
  titleKey?: string;
  titleArg?: string;
  detail?: string;
  /** i18n key for the stage detail line; detail is the fallback. */
  detailKey?: string;
  /** 0-100 for determinate; undefined for indeterminate spinner. */
  progress?: number;
  error?: string;
  cancellable: boolean;
  resumable: boolean;
  startedAt: string;
  finishedAt?: string;
}

interface ProcessState {
  processes: ProcessInfo[];
  setProcesses: (processes: ProcessInfo[]) => void;
}

export const useProcessStore = create<ProcessState>((set) => ({
  processes: [],
  setProcesses: (processes) => set({ processes }),
}));

/** Localized process title — backend titles are English; titleKey follows the UI language. */
export function processTitle(p: ProcessInfo, t: TFunction): string {
  return p.titleKey ? t(p.titleKey, { arg: p.titleArg ?? '', defaultValue: p.title }) : p.title;
}

/** Localized stage detail line (undefined when the process has none). */
export function processDetail(p: ProcessInfo, t: TFunction): string | undefined {
  if (p.detailKey) return t(p.detailKey, { defaultValue: p.detail ?? '' });
  return p.detail;
}

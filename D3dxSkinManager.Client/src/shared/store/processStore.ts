/**
 * Global process store — mirror of the backend ProcessRegistry (the authoritative source of all
 * long-running operations). Fed by the SYSTEM/PROCESS_LIST_UPDATED event snapshot via
 * initProcessBridge(). The status bar + Activity panel read from here.
 *
 * Supersedes the old ephemeral taskStore (which held only frontend-registered {id,label,progress}).
 */
import { create } from 'zustand';

// NOTE: enums are camelCase because IpcHandler serializes C# enums with
// JsonStringEnumConverter(CamelCase). See .claude/rules/enum-serialization.md.
export type ProcessStatus = 'queued' | 'running' | 'completed' | 'failed' | 'cancelled';

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
  | 'other';

export interface ProcessInfo {
  id: string;
  type: ProcessType;
  status: ProcessStatus;
  title: string;
  detail?: string;
  /** 0-100 for determinate; undefined for indeterminate spinner. */
  progress?: number;
  error?: string;
  cancellable: boolean;
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

/** Convenience selectors. */
export const selectRunning = (s: ProcessState) => s.processes.filter((p) => p.status === 'running');
export const selectFinished = (s: ProcessState) =>
  s.processes.filter((p) => p.status !== 'running');

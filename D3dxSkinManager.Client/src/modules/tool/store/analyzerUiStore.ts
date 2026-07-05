import { create } from 'zustand';

/**
 * Persistent UI state for the Mod Analyzer slide-in. The tool unmounts when closed (e.g. after
 * "locate in mod list"), which used to dump the user back at the scan screen with their session,
 * filter and search gone — they had to navigate back one row at a time. This store survives the
 * unmount so reopening restores exactly where they were. Reset when the profile changes.
 */
export type AnalyzerViewMode = 'scan' | 'findings' | 'history';
export type AnalyzerFindingFilter = 'all' | 'broken' | 'stale' | 'duplicates' | 'conflicts' | 'healthy';

interface AnalyzerUiState {
  profileId?: string;
  viewMode: AnalyzerViewMode;
  /** Session whose report was being viewed — refetched on reopen. */
  sessionId?: string;
  findingsFilter: AnalyzerFindingFilter;
  searchText: string;

  setViewMode: (mode: AnalyzerViewMode) => void;
  setSession: (sessionId?: string) => void;
  setFindingsFilter: (filter: AnalyzerFindingFilter) => void;
  setSearchText: (text: string) => void;
  /** Reset when the active profile changes (state from another profile is meaningless). */
  ensureProfile: (profileId: string) => void;
}

const initial = {
  viewMode: 'scan' as AnalyzerViewMode,
  sessionId: undefined,
  findingsFilter: 'all' as AnalyzerFindingFilter,
  searchText: '',
};

export const useAnalyzerUiStore = create<AnalyzerUiState>((set, get) => ({
  profileId: undefined,
  ...initial,

  setViewMode: (viewMode) => set({ viewMode }),
  setSession: (sessionId) => set({ sessionId }),
  setFindingsFilter: (findingsFilter) => set({ findingsFilter }),
  setSearchText: (searchText) => set({ searchText }),
  ensureProfile: (profileId) => {
    if (get().profileId === profileId) return;
    set({ profileId, ...initial });
  },
}));

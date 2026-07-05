/**
 * Remote-library UI state that must survive tab switches / view unmounts (the same lesson as
 * analyzerUiStore: losing your place after navigating away is the #1 workflow complaint).
 * Holds selection + the last browse result; no IPC here — the view drives refreshes.
 */

import { create } from 'zustand';
import type { RemoteBrowseResult } from '../../../shared/types/remote.types';

interface RemoteUiState {
  profileId?: string;
  sourceId?: string;
  listId?: string;
  page: number;
  searchText: string;
  /** Last browse/search result (undefined until the first load). */
  result?: RemoteBrowseResult;
  /** True when `result` came from a search rather than list browsing. */
  isSearchResult: boolean;

  setSource: (sourceId: string | undefined) => void;
  setList: (listId: string | undefined) => void;
  setPage: (page: number) => void;
  setSearchText: (text: string) => void;
  setResult: (result: RemoteBrowseResult | undefined, isSearchResult: boolean) => void;
  /** Reset when the profile changes — remote selection is per-profile context. */
  ensureProfile: (profileId: string) => void;
}

const initialState = {
  sourceId: undefined as string | undefined,
  listId: undefined as string | undefined,
  page: 1,
  searchText: '',
  result: undefined as RemoteBrowseResult | undefined,
  isSearchResult: false,
};

export const useRemoteUiStore = create<RemoteUiState>((set, get) => ({
  profileId: undefined,
  ...initialState,

  setSource: (sourceId) => set({ sourceId, listId: undefined, page: 1, result: undefined, isSearchResult: false }),
  setList: (listId) => set({ listId, page: 1, result: undefined, isSearchResult: false }),
  setPage: (page) => set({ page }),
  setSearchText: (searchText) => set({ searchText }),
  setResult: (result, isSearchResult) => set({ result, isSearchResult }),
  ensureProfile: (profileId) => {
    if (get().profileId === profileId) return;
    set({ profileId, ...initialState });
  },
}));

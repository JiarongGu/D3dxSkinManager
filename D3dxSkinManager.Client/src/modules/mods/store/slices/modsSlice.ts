/**
 * Mods slice - manages mod collection state
 */

import { ModInfo } from '../../../../shared/types/mod.types';

export interface ModsSliceState {
  mods: ModInfo[];
  loading: boolean;
  error: string | undefined;
  selectedMod: ModInfo | undefined;
  selectedMods: ModInfo[];
}

export const initialModsState: ModsSliceState = {
  mods: [],
  loading: false,
  error: undefined,
  selectedMod: undefined,
  selectedMods: [],
};

export interface ModsSliceActions {
  setMods: (mods: ModInfo[]) => void;
  setLoading: (loading: boolean) => void;
  setError: (error: string | undefined) => void;
  setSelectedMod: (mod: ModInfo | undefined) => void;
  setSelectedMods: (mods: ModInfo[]) => void;

  // Local updates
  updateModLocal: (sha: string, data: Partial<ModInfo>) => void;
  updateModsLocal: (shas: string[], data: Partial<ModInfo>) => void;
  addMod: (mod: ModInfo) => void;
  removeMod: (sha: string) => void;

  // Optimistic updates
  optimisticLoadUpdate: (sha: string, unloadedShas: string[]) => void;
  optimisticUnloadUpdate: (sha: string) => void;
  optimisticCategoryUpdate: (sha: string, categoryId: string) => void;

  // Reset
  reset: () => void;
}

export const createModsSliceActions = (
  set: (fn: (state: ModsSliceState) => Partial<ModsSliceState>) => void,
  get: () => ModsSliceState
): ModsSliceActions => ({
  setMods: (mods) => set(() => ({ mods })),

  setLoading: (loading) => set(() => ({ loading })),

  setError: (error) => set(() => ({ error })),

  setSelectedMod: (mod) => set(() => ({ selectedMod: mod })),

  setSelectedMods: (mods) => set(() => ({ selectedMods: mods })),

  updateModLocal: (sha, data) =>
    set((state) => ({
      mods: state.mods.map((mod) =>
        mod.sha === sha ? { ...mod, ...data } : mod
      ),
      selectedMod:
        state.selectedMod?.sha === sha
          ? { ...state.selectedMod, ...data }
          : state.selectedMod,
    })),

  updateModsLocal: (shas, data) =>
    set((state) => ({
      mods: state.mods.map((mod) =>
        shas.includes(mod.sha) ? { ...mod, ...data } : mod
      ),
      selectedMod:
        state.selectedMod && shas.includes(state.selectedMod.sha)
          ? { ...state.selectedMod, ...data }
          : state.selectedMod,
    })),

  addMod: (mod) =>
    set((state) => ({
      mods: [...state.mods, mod],
    })),

  removeMod: (sha) =>
    set((state) => ({
      mods: state.mods.filter((mod) => mod.sha !== sha),
      selectedMod: state.selectedMod?.sha === sha ? undefined : state.selectedMod,
      selectedMods: state.selectedMods.filter((mod) => mod.sha !== sha),
    })),

  optimisticLoadUpdate: (sha, unloadedShas) =>
    set((state) => ({
      mods: state.mods.map((mod) => {
        if (mod.sha === sha) {
          return { ...mod, isLoaded: true };
        }
        if (unloadedShas.includes(mod.sha)) {
          return { ...mod, isLoaded: false };
        }
        return mod;
      }),
    })),

  optimisticUnloadUpdate: (sha) =>
    set((state) => ({
      mods: state.mods.map((mod) =>
        mod.sha === sha ? { ...mod, isLoaded: false } : mod
      ),
    })),

  optimisticCategoryUpdate: (sha, categoryId) =>
    set((state) => ({
      mods: state.mods.map((mod) =>
        mod.sha === sha ? { ...mod, category: categoryId } : mod
      ),
    })),

  reset: () => set(() => initialModsState),
});

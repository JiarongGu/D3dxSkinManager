import { renderHook, act } from '@testing-library/react';

// useMods() reads selectedProfileId from ProfileContext — stub it (path relative to THIS test file).
vi.mock('../../../../shared/context/ProfileContext', () => ({
  useProfile: () => ({ selectedProfileId: 'p1' }),
}));

import { useModsStore } from '../../store/modsStore';
import { useModsState, useMods } from '../useMods';
import type { ModInfo } from '../../../../shared/types/mod.types';

const mod = (id: string): ModInfo => ({ id, name: id }) as ModInfo;

describe('useModsState', () => {
  beforeEach(() => {
    act(() => {
      useModsStore.setState({ mods: [], selectedMod: undefined, modLoading: false });
    });
  });

  // NOTE: these selectors dereference `s.mods` / `s.modLoading`. If the wrapper's selector param
  // ever regresses to `unknown` (the original bug — `ReturnType<typeof useModsStore>`), this file
  // FAILS TO COMPILE. So the suite is also the type-guard for the fix.
  it('returns the selected slice', () => {
    const m = mod('m1');
    act(() => useModsStore.setState({ mods: [m] }));
    const { result } = renderHook(() => useModsState((s) => s.mods));
    expect(result.current).toEqual([m]);
  });

  it('re-renders when the selected slice changes (reactive)', () => {
    const { result } = renderHook(() => useModsState((s) => s.modLoading));
    expect(result.current).toBe(false);
    act(() => useModsStore.setState({ modLoading: true }));
    expect(result.current).toBe(true);
  });

  it('keeps identity on an unrelated update (selector isolation)', () => {
    const { result } = renderHook(() => useModsState((s) => s.mods));
    const first = result.current;
    act(() => useModsStore.setState({ modLoading: true })); // unrelated slice
    expect(result.current).toBe(first); // same ref → this selector did not re-fire
  });
});

describe('useMods', () => {
  it('exposes state + operations + selectedProfileId', () => {
    const { result } = renderHook(() => useMods());
    expect(result.current.state).toBeDefined();
    expect(typeof result.current.refreshMods).toBe('function');
    expect(typeof result.current.selectMod).toBe('function');
    expect(result.current.selectedProfileId).toBe('p1');
  });
});

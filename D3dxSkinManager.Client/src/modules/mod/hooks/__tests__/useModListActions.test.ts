import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import type { ModInfo } from '../../../../shared/types/mod.types';

const deleteCache = vi.fn();
const updateArchiveFromCache = vi.fn();
const updateModSvc = vi.fn();
const openFileDialog = vi.fn();
const refreshMods = vi.fn();
const addBusyMod = vi.fn();
const removeBusyMod = vi.fn();
const busyRef = { set: new Set<string>() };

vi.mock('react-i18next', () => ({ useTranslation: () => ({ t: (k: string) => k, i18n: { language: 'en' } }) }));
vi.mock('../../../../shared/services/ipc', () => ({
  modService: {
    deleteCache: (...a: unknown[]) => deleteCache(...a),
    updateArchiveFromCache: (...a: unknown[]) => updateArchiveFromCache(...a),
    updateMod: (...a: unknown[]) => updateModSvc(...a),
  },
  systemService: { openFileDialog: (...a: unknown[]) => openFileDialog(...a) },
}));
vi.mock('../../../../shared/utils/notification', () => ({ notification: { success: vi.fn(), error: vi.fn(), info: vi.fn() } }));
vi.mock('../../../../shared/context/ProfileContext', () => ({ useProfile: () => ({ selectedProfileId: 'p1' }) }));
vi.mock('../../store/modsStore', () => ({ useModsStore: { getState: () => ({ addBusyMod, removeBusyMod }) } }));
vi.mock('../useMods', () => ({ useModsState: (sel: (s: { busyModIds: Set<string> }) => unknown) => sel({ busyModIds: busyRef.set }) }));
vi.mock('../../operations/modOperations', () => ({ refreshMods: (...a: unknown[]) => refreshMods(...a) }));

import { useModListActions } from '../useModListActions';

const mod = { id: 'm1', name: 'Mod One' } as ModInfo;

describe('useModListActions', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    busyRef.set = new Set();
    deleteCache.mockResolvedValue(true);
    updateArchiveFromCache.mockResolvedValue(undefined);
    updateModSvc.mockResolvedValue(undefined);
    openFileDialog.mockResolvedValue({ success: true, filePath: '/x.7z' });
  });

  it('deleteCachedMod deletes the cache + refreshes on success', async () => {
    const { result } = renderHook(() => useModListActions());
    await act(async () => { await result.current.deleteCachedMod(mod); });
    expect(deleteCache).toHaveBeenCalledWith('p1', 'm1');
    expect(refreshMods).toHaveBeenCalledWith('p1');
  });

  it('updateArchive marks busy, recompresses, then clears busy', async () => {
    const { result } = renderHook(() => useModListActions());
    await act(async () => { await result.current.updateArchive(mod); });
    expect(addBusyMod).toHaveBeenCalledWith('m1');
    expect(updateArchiveFromCache).toHaveBeenCalledWith('p1', 'm1');
    expect(removeBusyMod).toHaveBeenCalledWith('m1');
  });

  it('updateArchive is a no-op when the mod is already busy', async () => {
    busyRef.set = new Set(['m1']);
    const { result } = renderHook(() => useModListActions());
    await act(async () => { await result.current.updateArchive(mod); });
    expect(updateArchiveFromCache).not.toHaveBeenCalled();
  });

  it('updateMod picks a file then replaces the content + refreshes', async () => {
    const { result } = renderHook(() => useModListActions());
    await act(async () => { await result.current.updateMod(mod); });
    expect(openFileDialog).toHaveBeenCalled();
    expect(updateModSvc).toHaveBeenCalledWith('p1', 'm1', '/x.7z');
    expect(refreshMods).toHaveBeenCalledWith('p1');
  });

  it('updateMod aborts when the file dialog is cancelled', async () => {
    openFileDialog.mockResolvedValue({ success: false });
    const { result } = renderHook(() => useModListActions());
    await act(async () => { await result.current.updateMod(mod); });
    expect(updateModSvc).not.toHaveBeenCalled();
  });
});

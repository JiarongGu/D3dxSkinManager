import { describe, it, expect, beforeEach } from 'vitest';
import { useModsStore } from '../modsStore';
import type { ModInfo } from '../../../../shared/types/mod.types';
import type { CategoryInfo } from '../../../../shared/types/category.types';

const store = () => useModsStore.getState();
const mod = (id: string): ModInfo => ({ id, name: id } as ModInfo);
const cat = (id: string): CategoryInfo => ({ id, name: id } as CategoryInfo);

describe('modsStore', () => {
  beforeEach(() => {
    // Fresh: profileId undefined so reset doesn't cache/restore; data fields back to initial.
    store().reset(undefined);
  });

  describe('setters', () => {
    it('setModHealth stores the per-mod health map', () => {
      store().setModHealth({ a: { modId: 'a', healthStatus: 'error', issueCount: 3 } });
      expect(store().modHealth.a.healthStatus).toBe('error');
      expect(store().modHealth.a.issueCount).toBe(3);
    });

    it('setActiveMods stores the active list (drives the per-category indicator)', () => {
      store().setActiveMods([mod('x'), mod('y')]);
      expect(store().activeMods.map((m) => m.id)).toEqual(['x', 'y']);
    });

    it('setMods stores the filtered list', () => {
      store().setMods([mod('m1')]);
      expect(store().mods).toHaveLength(1);
    });
  });

  describe('per-mod busy tracking', () => {
    it('addBusyMod / isModBusy / removeBusyMod', () => {
      expect(store().isModBusy('m')).toBe(false);
      store().addBusyMod('m');
      expect(store().isModBusy('m')).toBe(true);
      store().removeBusyMod('m');
      expect(store().isModBusy('m')).toBe(false);
    });
  });

  describe('reset', () => {
    it('clears data fields back to initial', () => {
      store().setMods([mod('m')]);
      store().setActiveMods([mod('a')]);
      store().setModHealth({ a: { modId: 'a', healthStatus: 'warning', issueCount: 1 } });
      store().setSelectedMod(mod('m'));

      store().reset(undefined);

      expect(store().mods).toBeUndefined();
      expect(store().activeMods).toEqual([]);
      expect(store().modHealth).toEqual({});
      expect(store().selectedMod).toBeUndefined();
    });

    it('caches per-profile UI state and restores it on switch-back', () => {
      // Enter profile A and set some UI state.
      store().reset('p-A');
      store().setSelectedCategory(cat('catA'));
      store().setSearchQuery('hair');
      store().setExpandedKeys(['k1', 'k2']);

      // Switch to a fresh profile B → no cached state.
      store().reset('p-B');
      expect(store().selectedCategory).toBeUndefined();
      expect(store().searchQuery).toBe('');

      // Switch back to A → its UI state is restored from the per-profile cache.
      store().reset('p-A');
      expect(store().selectedCategory?.id).toBe('catA');
      expect(store().searchQuery).toBe('hair');
      expect(store().expandedKeys).toEqual(['k1', 'k2']);
    });
  });
});

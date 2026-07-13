import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import type { ContextMenuItem } from '../../../../shared/components/menu';

const getFixTools = vi.fn();
const runModFix = vi.fn();

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (k: string) => k, i18n: { language: 'en' } }),
}));
vi.mock('../../../../shared/services/ipc', () => ({
  toolService: { getFixTools: (...a: unknown[]) => getFixTools(...a), runModFix: (...a: unknown[]) => runModFix(...a) },
}));
vi.mock('../../../../shared/utils/notification', () => ({ notification: { info: vi.fn(), error: vi.fn() } }));
vi.mock('../../../../shared/context/ProfileContext', () => ({ useProfile: () => ({ selectedProfileId: 'p1' }) }));
vi.mock('../../../../shared/hooks/useEventSubscription', () => ({ useEventSubscription: () => {} }));

import { useModFixTools } from '../useModFixTools';

const tool = {
  id: 't1', name: 'ReFix', enabled: true, recompressDefault: false,
  entries: [{ name: 'run.bat', path: '/x/run.bat' }],
};

const kids = (item: ContextMenuItem): ContextMenuItem[] => (item as { children?: ContextMenuItem[] }).children ?? [];

describe('useModFixTools', () => {
  beforeEach(() => {
    getFixTools.mockReset().mockResolvedValue([tool]);
    runModFix.mockReset().mockResolvedValue(undefined);
  });

  it('builds a Fix submenu from the loaded tools (+ Manage)', async () => {
    const { result } = renderHook(() => useModFixTools(vi.fn()));
    await waitFor(() =>
      expect(kids(result.current.buildFixSubmenu(['m1'])).some((c) => (c as { label?: string }).label === 'ReFix')).toBe(true),
    );

    const sub = result.current.buildFixSubmenu(['m1']);
    expect(sub.key).toBe('run-fix');
    expect(kids(sub).some((c) => (c as { label?: string }).label === 'ReFix')).toBe(true);
    expect(kids(sub).some((c) => c.key === 'fix-manage')).toBe(true);
  });

  it('runs a tool entry against the passed mod ids', async () => {
    const { result } = renderHook(() => useModFixTools(vi.fn()));
    await waitFor(() =>
      expect(kids(result.current.buildFixSubmenu(['m1'])).some((c) => (c as { label?: string }).label === 'ReFix')).toBe(true),
    );

    const entry = kids(result.current.buildFixSubmenu(['m1', 'm2'])).find(
      (c) => (c as { label?: string }).label === 'ReFix',
    ) as { onClick?: () => void };
    entry.onClick?.();
    expect(runModFix).toHaveBeenCalledWith('p1', { scriptPath: '/x/run.bat', modIds: ['m1', 'm2'], recompress: false });
  });

  it('Manage calls onManage', async () => {
    const onManage = vi.fn();
    const { result } = renderHook(() => useModFixTools(onManage));
    await waitFor(() =>
      expect(kids(result.current.buildFixSubmenu([])).some((c) => c.key === 'fix-manage')).toBe(true),
    );
    // ensure tools finished loading (the ready signal) before asserting Manage
    await waitFor(() =>
      expect(kids(result.current.buildFixSubmenu([])).some((c) => (c as { label?: string }).label === 'ReFix')).toBe(true),
    );

    const manage = kids(result.current.buildFixSubmenu([])).find((c) => c.key === 'fix-manage') as { onClick?: () => void };
    manage.onClick?.();
    expect(onManage).toHaveBeenCalled();
  });

  it('bulkFixMenuItems lists the tools for the given mods', async () => {
    const { result } = renderHook(() => useModFixTools(vi.fn()));
    await waitFor(() =>
      expect((result.current.bulkFixMenuItems(['m1']) ?? []).some((i) => (i as { label?: string }).label === 'ReFix')).toBe(true),
    );
    const labels = (result.current.bulkFixMenuItems(['m1']) ?? []).map((i) => (i as { label?: string }).label);
    expect(labels).toContain('ReFix');
  });
});

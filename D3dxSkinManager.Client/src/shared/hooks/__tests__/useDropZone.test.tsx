import React from 'react';
import { renderHook } from '@testing-library/react';
import { useDropZone } from '../useDropZone';
import { bridgeService } from '../../services/bridgeService';

// Lifecycle edge cases (code-review F3/F4): the zone must UNREGISTER when the effect tears down —
// on unmount AND when `enabled` flips false — including while the REGISTER is still in flight
// (otherwise a fast mount→unmount orphans the backend overlay). sendMessage returns a NEVER-resolving
// promise so REGISTER stays "in flight" (isRegisteredRef never set), exercising exactly that case.
vi.mock('../../services/bridgeService', () => ({
  bridgeService: { sendMessage: vi.fn(() => new Promise(() => {})) },
}));

const dzCalls = (type: string) =>
  (bridgeService.sendMessage as ReturnType<typeof vi.fn>).mock.calls
    .map((c) => c[0] as { module: string; type: string })
    .filter((m) => m.module === 'DROP_ZONE' && m.type === type);

describe('useDropZone lifecycle', () => {
  let el: HTMLDivElement;

  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers();
    el = document.createElement('div');
    document.body.appendChild(el);
  });

  afterEach(() => {
    vi.useRealTimers();
    el.remove();
  });

  const ref = () => ({ current: el }) as React.RefObject<HTMLElement>;

  it('registers the zone on mount (after the bounds debounce)', () => {
    renderHook(() => useDropZone({ targetRef: ref(), onDrop: vi.fn() }));
    vi.advanceTimersByTime(150); // flush the 100ms updateZoneBounds debounce
    expect(dzCalls('REGISTER').length).toBeGreaterThanOrEqual(1);
  });

  it('unregisters on unmount even while REGISTER is still in flight (F3)', () => {
    const r = ref();
    const { unmount } = renderHook(() => useDropZone({ targetRef: r, onDrop: vi.fn() }));
    vi.advanceTimersByTime(150);
    expect(dzCalls('REGISTER').length).toBeGreaterThanOrEqual(1);

    unmount();

    // Must unregister despite the REGISTER never resolving — otherwise the backend overlay orphans.
    expect(dzCalls('UNREGISTER').length).toBe(1);
    expect(dzCalls('UNREGISTER')[0]).toMatchObject({ module: 'DROP_ZONE', type: 'UNREGISTER' });
  });

  it('unregisters when `enabled` flips to false (F4)', () => {
    const r = ref();
    const { rerender } = renderHook(
      ({ enabled }: { enabled: boolean }) => useDropZone({ targetRef: r, onDrop: vi.fn(), enabled }),
      { initialProps: { enabled: true } },
    );
    vi.advanceTimersByTime(150);
    expect(dzCalls('REGISTER').length).toBeGreaterThanOrEqual(1);

    rerender({ enabled: false });

    expect(dzCalls('UNREGISTER').length).toBe(1);
  });

  it('does not register when disabled from the start', () => {
    renderHook(() => useDropZone({ targetRef: ref(), onDrop: vi.fn(), enabled: false }));
    vi.advanceTimersByTime(150);
    expect(dzCalls('REGISTER').length).toBe(0);
  });
});

import { describe, it, expect, beforeEach, vi } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useInfiniteScroll } from '../useInfiniteScroll';

// jsdom has no IntersectionObserver — stub it and capture the callback so tests can fire "intersecting".
let ioCallback: IntersectionObserverCallback = () => {};
beforeEach(() => {
  ioCallback = () => {};
  vi.stubGlobal(
    'IntersectionObserver',
    class {
      constructor(cb: IntersectionObserverCallback) { ioCallback = cb; }
      observe() {}
      unobserve() {}
      disconnect() {}
      takeRecords() { return []; }
    },
  );
});

const intersect = () =>
  act(() => ioCallback([{ isIntersecting: true } as IntersectionObserverEntry], {} as IntersectionObserver));

const range = (n: number) => Array.from({ length: n }, (_, i) => i);

describe('useInfiniteScroll', () => {
  it('shows the first batch (50) by default', () => {
    const { result } = renderHook(() => useInfiniteScroll(range(200)));
    expect(result.current.visibleCount).toBe(50);
    expect(result.current.visibleItems).toHaveLength(50);
  });

  it('respects minCount (forces at least that many rendered)', () => {
    const { result } = renderHook(() => useInfiniteScroll(range(200), 120));
    expect(result.current.visibleCount).toBe(120);
    expect(result.current.visibleItems).toHaveLength(120);
  });

  it('grows by a batch when the sentinel intersects', () => {
    const { result } = renderHook(() => useInfiniteScroll(range(200)));
    intersect();
    expect(result.current.visibleCount).toBe(100);
    intersect();
    expect(result.current.visibleCount).toBe(150);
  });

  it('never grows past the total', () => {
    const { result } = renderHook(() => useInfiniteScroll(range(3)));
    expect(result.current.visibleItems).toHaveLength(3);
    intersect();
    expect(result.current.visibleItems).toHaveLength(3);
  });

  it('resets to a batch when the list LENGTH changes (category switch / search)', () => {
    const { result, rerender } = renderHook(({ arr }) => useInfiniteScroll(arr), {
      initialProps: { arr: range(200) },
    });
    intersect();
    expect(result.current.visibleCount).toBe(100);
    rerender({ arr: range(150) }); // different length → reset
    expect(result.current.visibleItems).toHaveLength(50);
  });
});

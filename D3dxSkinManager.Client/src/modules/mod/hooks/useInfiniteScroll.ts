import React, { useCallback, useEffect, useRef, useState } from 'react';

/** Items loaded per batch when the sentinel scrolls into view. */
const BATCH = 50;

export interface InfiniteScroll<T> {
  /** The first `visibleCount` of `items` — render these. */
  visibleItems: T[];
  /** Attach to a bottom sentinel element; scrolling it into view loads the next batch. */
  sentinelRef: React.RefObject<HTMLDivElement | null>;
  /** How many items are currently shown (always >= `minCount`). */
  visibleCount: number;
}

/**
 * Windowed rendering with an IntersectionObserver "load more". Shows the first `visibleCount` items,
 * grows by BATCH when the sentinel enters the viewport, and resets to BATCH when the list LENGTH changes
 * (category switch / search) — but NOT on per-item property updates (isLoaded, …), which would flash the
 * scroll position. `minCount` forces at least that many rendered (used to scroll to an item not yet in
 * the DOM). Extracted verbatim from ModList (behavior-preserving).
 */
export function useInfiniteScroll<T>(items: T[], minCount = 0): InfiniteScroll<T> {
  const [displayCount, setDisplayCount] = useState(BATCH);
  const sentinelRef = useRef<HTMLDivElement>(null);
  const visibleCount = Math.max(displayCount, minCount);
  const total = items.length;

  const handleObserver = useCallback(
    (entries: IntersectionObserverEntry[]) => {
      const target = entries[0];
      if (target.isIntersecting && visibleCount < total) {
        // Grow from visibleCount (not displayCount) so a forced minCount render is accounted for.
        setDisplayCount(Math.min(visibleCount + BATCH, total));
      }
    },
    [visibleCount, total],
  );

  useEffect(() => {
    // rootMargin pre-loads a batch early; threshold 0 fires as soon as any pixel of the (possibly very
    // tall) sentinel enters view — a non-zero threshold would need N% of it visible, unusable here.
    const observer = new IntersectionObserver(handleObserver, { root: null, rootMargin: '100px', threshold: 0 });
    const el = sentinelRef.current;
    if (el) observer.observe(el);
    return () => { if (el) observer.unobserve(el); };
  }, [handleObserver]);

  // Reset ONLY when the length changes (category change, search) — not on property updates, to avoid a
  // flash during infinite scroll.
  useEffect(() => {
    setDisplayCount(BATCH);
  }, [total]);

  return { visibleItems: items.slice(0, visibleCount), sentinelRef, visibleCount };
}

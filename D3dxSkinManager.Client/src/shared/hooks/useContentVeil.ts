import { useEffect, useRef, useState } from 'react';
import { systemService } from '../services/ipc';
import { useSettingsStore } from '../../modules/setting/store/settingsStore';
import { logger } from '../utils/logger';

/**
 * Content-veil verdicts for a set of image urls (backend pure-CPU sensitivity heuristic,
 * batched over one IPC call).
 *
 * Contract for consumers (veil-until-verdict — no flash of sensitive content):
 *   veiled = enabled && verdict !== 'safe' && verdict !== 'unknown'
 * i.e. while a verdict is PENDING the image stays veiled; 'unknown' (unresolvable/undecodable)
 * reveals — the heuristic can't judge it, so don't punish it forever.
 *
 * Verdicts are cached module-wide (the backend caches per (file, mtime) too) so revisiting a page
 * never re-asks. When the global toggle is off the hook is inert.
 *
 * INCREMENTAL: verdicts stream in small chunks (a few concurrent requests) and the consumer
 * re-renders after EACH chunk resolves — cards un-veil in waves instead of all-at-once after the
 * slowest image (the AI plugin runs ~tens of ms/image, so a one-shot batch felt laggy).
 */

const verdictCache = new Map<string, string>();

// Small chunks + bounded concurrency = frequent partial updates without flooding the IPC bridge.
const CHUNK_SIZE = 6;
const MAX_CONCURRENT_CHUNKS = 3;

export function useContentVeilEnabled(): boolean {
  return useSettingsStore((s) => s.globalSettings?.contentVeilEnabled ?? false);
}

export function useContentVeilVerdicts(urls: (string | undefined)[]): Record<string, string> {
  const enabled = useContentVeilEnabled();
  const [, bump] = useState(0);
  const pendingRef = useRef<Set<string>>(new Set());

  // Stable key so the effect re-runs only when the actual url set changes.
  const wanted = enabled ? urls.filter((u): u is string => !!u) : [];
  const key = wanted.join('\n');

  useEffect(() => {
    if (!enabled) return;
    const missing = wanted.filter((u) => !verdictCache.has(u) && !pendingRef.current.has(u));
    if (missing.length === 0) return;

    missing.forEach((u) => pendingRef.current.add(u));
    let cancelled = false;

    // Split into small chunks and run a few at a time; bump after each so the UI un-veils
    // progressively rather than waiting for the whole set.
    const chunks: string[][] = [];
    for (let i = 0; i < missing.length; i += CHUNK_SIZE) chunks.push(missing.slice(i, i + CHUNK_SIZE));

    const runChunk = async (chunk: string[]) => {
      try {
        const verdicts = await systemService.checkContentVeil(chunk);
        Object.entries(verdicts).forEach(([u, v]) => verdictCache.set(u, v));
      } catch (error) {
        // Chunk failure: mark unknown so those images don't stay veiled forever.
        chunk.forEach((u) => verdictCache.set(u, 'unknown'));
        logger.warn('[useContentVeil] check failed:', error);
      } finally {
        chunk.forEach((u) => pendingRef.current.delete(u));
      }
      if (!cancelled) bump((n) => n + 1); // incremental re-render per chunk
    };

    // Worker pool: MAX_CONCURRENT_CHUNKS pull from the chunk queue until drained.
    let next = 0;
    const worker = async () => {
      while (!cancelled && next < chunks.length) {
        const chunk = chunks[next++];
        await runChunk(chunk);
      }
    };
    void Promise.all(Array.from({ length: Math.min(MAX_CONCURRENT_CHUNKS, chunks.length) }, worker));

    return () => {
      cancelled = true;
      // Release urls this run CLAIMED but never resolved. Otherwise they stay stuck in pendingRef
      // (a cancelled worker suppresses its bump AND leaves not-yet-run chunks marked pending), so a
      // re-run's `missing` filter skips them and they never re-fetch — the card stays veiled until a
      // remount. This bit hard under React StrictMode: its mount→cleanup→mount double-invoke cancelled
      // the FIRST run, orphaning every url, so the whole page stayed veiled until a tab change gave a
      // fresh pendingRef (user report). Releasing here lets the second run re-request them.
      missing.forEach((u) => { if (!verdictCache.has(u)) pendingRef.current.delete(u); });
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [enabled, key]);

  if (!enabled) return {};
  const result: Record<string, string> = {};
  wanted.forEach((u) => {
    const v = verdictCache.get(u);
    if (v) result[u] = v;
  });
  return result;
}

/** The veil decision for one url under the veil-until-verdict contract. */
export function isVeiled(enabled: boolean, verdict: string | undefined): boolean {
  return enabled && verdict !== 'safe' && verdict !== 'unknown';
}

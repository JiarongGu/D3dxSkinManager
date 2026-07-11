/**
 * Progress percent (0–100), rounded and zero-guarded.
 *
 * Returns 0 when `total` is 0/negative/undefined so callers never produce NaN/Infinity from a
 * divide-by-zero (several call sites previously guarded inconsistently). Use for any
 * current/total → percent conversion (scan/import/export/migration progress bars).
 */
export function toPercent(current: number, total: number): number {
  if (!total || total <= 0) return 0;
  return Math.round((current / total) * 100);
}

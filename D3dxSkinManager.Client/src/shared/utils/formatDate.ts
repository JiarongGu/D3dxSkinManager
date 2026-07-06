/**
 * Locale-aware date/time formatting — the single place these live (was inlined as
 * `new Date(x).toLocaleString()` in 7 spots, none guarding invalid/empty input).
 * Empty or unparseable input → '' (never "Invalid Date").
 */

function toDate(value?: string | number | Date | null): Date | undefined {
  if (value === undefined || value === null || value === '') return undefined;
  const d = value instanceof Date ? value : new Date(value);
  return isNaN(d.getTime()) ? undefined : d;
}

/** Locale date + time (e.g. "1/2/2026, 3:04:05 PM"). Empty/invalid → ''. */
export function formatDateTime(value?: string | number | Date | null): string {
  return toDate(value)?.toLocaleString() ?? '';
}

/** Locale date only (e.g. "1/2/2026"). Empty/invalid → ''. */
export function formatDate(value?: string | number | Date | null): string {
  return toDate(value)?.toLocaleDateString() ?? '';
}

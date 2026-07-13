/**
 * Display helpers for remote site tags (remote-library-redesign.md).
 * Raw tag names are the stored/filter/rule identity; ONLY display maps through the source's
 * per-language label table (configurable i18n — user-editable in library management).
 */

/** The display label for a raw tag in the given app language, falling back to the raw name. */
export function remoteTagLabel(
  tagLabels: Record<string, Record<string, string>> | undefined,
  lang: string,
  tag: string,
): string {
  return tagLabels?.[lang]?.[tag] ?? tag;
}

/** Display order: most specific first (the sub category is merged AFTER the super, so reverse). */
export function orderTagsForDisplay(tags: string[]): string[] {
  return [...tags].reverse();
}

/**
 * Map raw tags → display labels in order, DEDUPED by resolved label: two raw tags that map to the
 * same label (A,B → C) collapse to ONE entry (first occurrence wins). `key`/`label` are both the
 * label (unique after dedup). Fixes duplicate identical tag chips.
 */
export function remoteTagLabelsDeduped(
  tagLabels: Record<string, Record<string, string>> | undefined,
  lang: string,
  tags: string[],
): { key: string; label: string }[] {
  const seen = new Set<string>();
  const out: { key: string; label: string }[] = [];
  for (const tag of tags) {
    const label = remoteTagLabel(tagLabels, lang, tag);
    if (seen.has(label)) continue;
    seen.add(label);
    out.push({ key: label, label });
  }
  return out;
}

/**
 * Merge raw tag counts by resolved display label (A,B → C become ONE chip, counts summed). Order =
 * first appearance. The `label` is the filter value — the backend expands a label back to its raw
 * tags (exact match), so selecting a merged chip filters by every underlying tag.
 */
export function mergeTagCountsByLabel(
  tagCounts: { name: string; count: number }[],
  tagLabels: Record<string, Record<string, string>> | undefined,
  lang: string,
): { label: string; count: number }[] {
  const byLabel = new Map<string, number>();
  const order: string[] = [];
  for (const tc of tagCounts) {
    const label = remoteTagLabel(tagLabels, lang, tc.name);
    if (!byLabel.has(label)) order.push(label);
    byLabel.set(label, (byLabel.get(label) ?? 0) + tc.count);
  }
  return order.map((label) => ({ label, count: byLabel.get(label)! }));
}

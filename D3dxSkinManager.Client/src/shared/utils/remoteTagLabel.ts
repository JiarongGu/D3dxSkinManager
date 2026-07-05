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

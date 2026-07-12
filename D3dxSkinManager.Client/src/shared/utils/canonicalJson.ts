/**
 * Canonical JSON string with recursively sorted object keys → stable, order-independent equality.
 *
 * Two objects that differ ONLY in key insertion order produce the SAME string, so this is the basis for
 * dirty-tracking: compare a form's current value against its saved baseline regardless of the order the
 * fields were assigned. Arrays keep their order (order is meaningful for ordered lists like input rules),
 * so `[1,2]` and `[2,1]` are NOT equal.
 *
 * Shared by the remote source + library editors (was duplicated as `canonical`/`canon`).
 * See .claude/knowledge/shared-utilities.md.
 */
export const canonicalJson = (value: unknown): string =>
  JSON.stringify(value, (_k, v) =>
    v && typeof v === 'object' && !Array.isArray(v)
      ? Object.keys(v as object)
          .sort()
          .reduce((acc, k) => {
            (acc as Record<string, unknown>)[k] = (v as Record<string, unknown>)[k];
            return acc;
          }, {} as Record<string, unknown>)
      : v,
  );

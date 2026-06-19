/**
 * Search Query Parser
 *
 * Parses a search string into a structured query supporting:
 *   - Implicit AND: `hair blue` matches items containing BOTH "hair" AND "blue"
 *   - OR operator:  `hair | skin` matches items containing "hair" OR "skin"
 *   - NOT prefix:   `-nsfw` excludes items containing "nsfw"
 *   - Exact phrase:  `"blue hair"` matches the exact phrase "blue hair"
 *   - Field prefix:  `tag:x` / `author:x` / `name:x` / `id:x` restricts a term to one field
 *
 * Precedence: `|` splits OR-groups; within each group terms are AND'd.
 *
 * Examples:
 *   "hair skin"            → items with BOTH "hair" AND "skin" (any field)
 *   "hair | skin"          → items with "hair" OR "skin"
 *   "hair -nsfw"           → items with "hair" but NOT "nsfw"
 *   "tag:hair author:john" → tag contains "hair" AND author contains "john"
 *   '"blue hair" | skin'   → exact phrase "blue hair" OR "skin"
 */

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export type SearchField = 'name' | 'author' | 'tag' | 'id';

export interface SearchTerm {
  /** The text to match (already lowercased) */
  text: string;
  /** Restrict match to a specific field; undefined = match any field */
  field?: SearchField;
  /** If true, the item must NOT match this term */
  negated: boolean;
  /** If true, match the exact phrase (no substring matching) */
  exact: boolean;
}

/** A group of terms that are AND'd together */
export interface SearchGroup {
  terms: SearchTerm[];
}

/** Top-level parsed query — groups are OR'd */
export interface ParsedQuery {
  groups: SearchGroup[];
  /** True when the raw input was empty / whitespace-only */
  isEmpty: boolean;
}

// ---------------------------------------------------------------------------
// Field accessor — maps SearchField to the value(s) to search in
// ---------------------------------------------------------------------------

export interface SearchableRecord {
  id: string;
  name: string;
  author?: string;
  tags?: string[];
  /** Additional searchable text (category name, description, etc.) */
  extra?: string[];
}

// ---------------------------------------------------------------------------
// Parser
// ---------------------------------------------------------------------------

// English prefixes always active as baseline.
const DEFAULT_PREFIXES: [string, SearchField][] = [
  ['author:', 'author'],
  ['name:', 'name'],
  ['tag:', 'tag'],
  ['id:', 'id'],
];

/**
 * Build the full prefix list by merging caller-supplied localized prefixes
 * with the built-in English defaults. Sorted longest-first so longer prefixes
 * match before shorter ones (e.g. "author:" before a hypothetical "a:").
 */
function buildPrefixes(localized?: Record<string, SearchField>): [string, SearchField][] {
  const merged = new Map<string, SearchField>();
  // Defaults first
  for (const [prefix, field] of DEFAULT_PREFIXES) {
    merged.set(prefix, field);
  }
  // Localized overrides / additions
  if (localized) {
    for (const [prefix, field] of Object.entries(localized)) {
      merged.set(prefix, field);
    }
  }
  // Sort longest first for greedy matching
  return [...merged.entries()].sort((a, b) => b[0].length - a[0].length);
}

/**
 * Tokenize a single OR-group string into SearchTerm[].
 *
 * Handles:
 *  - Quoted phrases: `"blue hair"`
 *  - Negation prefix: `-term` or `-"phrase"`
 *  - Field prefix: `tag:term` or `tag:"phrase"`
 *  - Bare words split by whitespace
 */
function tokenizeGroup(raw: string, prefixes: [string, SearchField][]): SearchTerm[] {
  const terms: SearchTerm[] = [];
  let i = 0;
  const s = raw.trim();

  while (i < s.length) {
    // Skip whitespace
    if (s[i] === ' ' || s[i] === '\t') {
      i++;
      continue;
    }

    let negated = false;
    let field: SearchField | undefined;
    let exact = false;
    let text = '';

    // Check negation prefix
    if (s[i] === '-' && i + 1 < s.length && s[i + 1] !== ' ') {
      negated = true;
      i++;
    }

    // Check field prefix (tag:, author:, name:, id:, + localized prefixes)
    for (const [prefix, fieldName] of prefixes) {
      if (s.substring(i, i + prefix.length).toLowerCase() === prefix) {
        field = fieldName;
        i += prefix.length;
        break;
      }
    }

    // Check for quoted phrase
    if (i < s.length && s[i] === '"') {
      exact = true;
      i++; // skip opening quote
      const closeIdx = s.indexOf('"', i);
      if (closeIdx === -1) {
        // No closing quote — treat rest as phrase
        text = s.substring(i);
        i = s.length;
      } else {
        text = s.substring(i, closeIdx);
        i = closeIdx + 1;
      }
    } else {
      // Bare word — read until whitespace
      const start = i;
      while (i < s.length && s[i] !== ' ' && s[i] !== '\t') {
        i++;
      }
      text = s.substring(start, i);
    }

    // A standalone "-" (a stray negation char with nothing after it) is a no-op, not a literal search.
    if (text && text !== '-') {
      terms.push({
        text: text.toLowerCase(),
        field,
        negated,
        exact,
      });
    }
  }

  return terms;
}

/**
 * Parse a raw search query string into a structured ParsedQuery.
 *
 * @param query - The raw user input from the search field
 * @param localizedPrefixes - Optional map of localized field prefixes from i18n
 *   e.g. `{ '标签:': 'tag', '作者:': 'author' }`. English defaults always included.
 * @returns ParsedQuery with OR-groups, each containing AND'd terms
 */
export function parseSearchQuery(
  query: string,
  localizedPrefixes?: Record<string, SearchField>,
): ParsedQuery {
  const trimmed = query.trim();
  if (!trimmed) {
    return { groups: [], isEmpty: true };
  }

  const prefixes = buildPrefixes(localizedPrefixes);

  // Split by | (pipe) for OR groups
  const rawGroups = trimmed.split('|');
  const groups: SearchGroup[] = [];

  for (const rawGroup of rawGroups) {
    const terms = tokenizeGroup(rawGroup, prefixes);
    if (terms.length > 0) {
      groups.push({ terms });
    }
  }

  return {
    groups,
    isEmpty: groups.length === 0,
  };
}

// ---------------------------------------------------------------------------
// Matcher
// ---------------------------------------------------------------------------

/**
 * Get the searchable text for a specific field from a record.
 */
function getFieldValues(record: SearchableRecord, field: SearchField): string[] {
  switch (field) {
    case 'name':
      return [record.name];
    case 'author':
      return record.author ? [record.author] : [];
    case 'tag':
      return record.tags ?? [];
    case 'id':
      return [record.id];
  }
}

/**
 * Get all searchable text from a record (for unqualified terms).
 */
function getAllValues(record: SearchableRecord): string[] {
  const values = [record.id, record.name];
  if (record.author) values.push(record.author);
  if (record.tags) values.push(...record.tags);
  if (record.extra) values.push(...record.extra);
  return values;
}

/**
 * Check if a single term matches a record.
 * ID field always uses exact match (IDs are long hashes, substring would give false positives).
 */
function termMatches(term: SearchTerm, record: SearchableRecord): boolean {
  // ID-specific search: always exact match
  if (term.field === 'id') {
    const match = record.id.toLowerCase() === term.text;
    return term.negated ? !match : match;
  }

  // Unqualified search: check all fields, but ID uses exact match
  if (!term.field) {
    const idMatch = record.id.toLowerCase() === term.text;
    const otherValues: string[] = [record.name];
    if (record.author) otherValues.push(record.author);
    if (record.tags) otherValues.push(...record.tags);
    if (record.extra) otherValues.push(...record.extra);

    const otherMatch = otherValues.some((val) => {
      const lower = val.toLowerCase();
      return term.exact ? lower === term.text : lower.includes(term.text);
    });

    const match = idMatch || otherMatch;
    return term.negated ? !match : match;
  }

  // Field-specific search (non-ID): normal matching
  const values = getFieldValues(record, term.field);
  const match = values.some((val) => {
    const lower = val.toLowerCase();
    return term.exact ? lower === term.text : lower.includes(term.text);
  });

  return term.negated ? !match : match;
}

/**
 * Check if a search group (AND'd terms) matches a record.
 * All terms in the group must match.
 */
function groupMatches(group: SearchGroup, record: SearchableRecord): boolean {
  return group.terms.every((term) => termMatches(term, record));
}

/**
 * Check if a parsed query matches a searchable record.
 * Returns true if ANY group matches (OR logic).
 *
 * @param query - A parsed query from parseSearchQuery()
 * @param record - The record to test
 * @returns true if the record matches the query
 */
export function matchesSearchQuery(query: ParsedQuery, record: SearchableRecord): boolean {
  if (query.isEmpty) return true;
  return query.groups.some((group) => groupMatches(group, record));
}

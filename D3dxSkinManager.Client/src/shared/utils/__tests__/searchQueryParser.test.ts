import {
  parseSearchQuery,
  matchesSearchQuery,
  SearchableRecord,
  ParsedQuery,
} from '../searchQueryParser';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function mod(overrides: Partial<SearchableRecord> = {}): SearchableRecord {
  return {
    id: 'abc123',
    name: 'Blue Hair Mod',
    author: 'JohnDoe',
    tags: ['hair', 'blue', 'cosmetic'],
    ...overrides,
  };
}

function matches(query: string, record: SearchableRecord): boolean {
  return matchesSearchQuery(parseSearchQuery(query), record);
}

// ---------------------------------------------------------------------------
// parseSearchQuery
// ---------------------------------------------------------------------------

describe('parseSearchQuery', () => {
  it('returns isEmpty for empty string', () => {
    expect(parseSearchQuery('').isEmpty).toBe(true);
    expect(parseSearchQuery('   ').isEmpty).toBe(true);
  });

  it('parses a single bare term', () => {
    const q = parseSearchQuery('hair');
    expect(q.isEmpty).toBe(false);
    expect(q.groups).toHaveLength(1);
    expect(q.groups[0].terms).toHaveLength(1);
    expect(q.groups[0].terms[0]).toMatchObject({
      text: 'hair',
      negated: false,
      exact: false,
      field: undefined,
    });
  });

  it('parses multiple AND terms (space-separated)', () => {
    const q = parseSearchQuery('hair blue');
    expect(q.groups).toHaveLength(1);
    expect(q.groups[0].terms).toHaveLength(2);
    expect(q.groups[0].terms[0].text).toBe('hair');
    expect(q.groups[0].terms[1].text).toBe('blue');
  });

  it('parses OR groups (pipe-separated)', () => {
    const q = parseSearchQuery('hair | skin');
    expect(q.groups).toHaveLength(2);
    expect(q.groups[0].terms[0].text).toBe('hair');
    expect(q.groups[1].terms[0].text).toBe('skin');
  });

  it('parses negation prefix', () => {
    const q = parseSearchQuery('-nsfw');
    expect(q.groups[0].terms[0]).toMatchObject({
      text: 'nsfw',
      negated: true,
    });
  });

  it('parses quoted exact phrase', () => {
    const q = parseSearchQuery('"blue hair"');
    expect(q.groups[0].terms[0]).toMatchObject({
      text: 'blue hair',
      exact: true,
    });
  });

  it('parses field prefix', () => {
    const q = parseSearchQuery('tag:hair');
    expect(q.groups[0].terms[0]).toMatchObject({
      text: 'hair',
      field: 'tag',
    });
  });

  it('parses field prefix with quoted value', () => {
    const q = parseSearchQuery('author:"John Doe"');
    expect(q.groups[0].terms[0]).toMatchObject({
      text: 'john doe',
      field: 'author',
      exact: true,
    });
  });

  it('parses negated field prefix', () => {
    const q = parseSearchQuery('-tag:nsfw');
    expect(q.groups[0].terms[0]).toMatchObject({
      text: 'nsfw',
      field: 'tag',
      negated: true,
    });
  });

  it('parses complex query with mixed operators', () => {
    const q = parseSearchQuery('tag:hair -nsfw | author:jane "red skin"');
    expect(q.groups).toHaveLength(2);
    // Group 1: tag:hair -nsfw
    expect(q.groups[0].terms).toHaveLength(2);
    expect(q.groups[0].terms[0]).toMatchObject({ text: 'hair', field: 'tag' });
    expect(q.groups[0].terms[1]).toMatchObject({ text: 'nsfw', negated: true });
    // Group 2: author:jane "red skin"
    expect(q.groups[1].terms).toHaveLength(2);
    expect(q.groups[1].terms[0]).toMatchObject({ text: 'jane', field: 'author' });
    expect(q.groups[1].terms[1]).toMatchObject({ text: 'red skin', exact: true });
  });

  it('handles unclosed quote gracefully', () => {
    const q = parseSearchQuery('"blue hair');
    expect(q.groups[0].terms[0]).toMatchObject({
      text: 'blue hair',
      exact: true,
    });
  });

  it('lowercases all term text', () => {
    const q = parseSearchQuery('HAIR Tag:BLUE');
    expect(q.groups[0].terms[0].text).toBe('hair');
    expect(q.groups[0].terms[1].text).toBe('blue');
  });

  it('skips empty OR groups', () => {
    const q = parseSearchQuery('hair | | skin');
    expect(q.groups).toHaveLength(2);
    expect(q.groups[0].terms[0].text).toBe('hair');
    expect(q.groups[1].terms[0].text).toBe('skin');
  });
});

// ---------------------------------------------------------------------------
// matchesSearchQuery — basic matching
// ---------------------------------------------------------------------------

describe('matchesSearchQuery', () => {
  it('matches everything when query is empty', () => {
    expect(matches('', mod())).toBe(true);
  });

  it('matches by name substring', () => {
    expect(matches('blue', mod())).toBe(true);
    expect(matches('hair', mod())).toBe(true);
    expect(matches('green', mod())).toBe(false);
  });

  it('matches by author substring', () => {
    expect(matches('john', mod())).toBe(true);
    expect(matches('jane', mod())).toBe(false);
  });

  it('matches by tag', () => {
    expect(matches('cosmetic', mod())).toBe(true);
    expect(matches('weapon', mod())).toBe(false);
  });

  it.skip('matches by id', () => {
    expect(matches('abc', mod())).toBe(true);
    expect(matches('xyz', mod())).toBe(false);
  });

  it('matches by extra field', () => {
    expect(matches('mycategory', mod({ extra: ['MyCategory'] }))).toBe(true);
  });

  it('is case-insensitive', () => {
    expect(matches('BLUE', mod())).toBe(true);
    expect(matches('johndoe', mod())).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// matchesSearchQuery — AND logic
// ---------------------------------------------------------------------------

describe('AND logic', () => {
  it('requires all terms to match', () => {
    expect(matches('blue hair', mod())).toBe(true);
    expect(matches('blue green', mod())).toBe(false);
  });

  it('AND across different fields', () => {
    expect(matches('blue john', mod())).toBe(true); // name + author
    expect(matches('hair cosmetic', mod())).toBe(true); // name + tag
  });
});

// ---------------------------------------------------------------------------
// matchesSearchQuery — OR logic
// ---------------------------------------------------------------------------

describe('OR logic', () => {
  it('matches if any group matches', () => {
    expect(matches('green | blue', mod())).toBe(true);
    expect(matches('green | red', mod())).toBe(false);
  });

  it('OR groups with AND terms inside', () => {
    // Group 1: green AND hair (fails), Group 2: blue AND hair (passes)
    expect(matches('green hair | blue hair', mod())).toBe(true);
    expect(matches('green hair | red skin', mod())).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// matchesSearchQuery — NOT logic
// ---------------------------------------------------------------------------

describe('NOT logic', () => {
  it('excludes matching items', () => {
    expect(matches('-green', mod())).toBe(true); // no "green" → passes
    expect(matches('-blue', mod())).toBe(false); // has "blue" → excluded
  });

  it('combines with AND', () => {
    expect(matches('hair -nsfw', mod())).toBe(true);
    expect(matches('hair -blue', mod())).toBe(false); // has hair but also blue
  });

  it('combines with OR', () => {
    expect(matches('-blue | hair', mod())).toBe(true); // second group matches
  });
});

// ---------------------------------------------------------------------------
// matchesSearchQuery — exact phrase
// ---------------------------------------------------------------------------

describe('exact phrase', () => {
  it('matches exact tag value', () => {
    expect(matches('"hair"', mod())).toBe(true); // tag "hair" matches exactly
    expect(matches('"hai"', mod())).toBe(false); // substring doesn't match exact
  });

  it('matches exact name', () => {
    expect(matches('"Blue Hair Mod"', mod())).toBe(true);
    expect(matches('"Blue Hair"', mod())).toBe(false); // not exact full value
  });

  it('matches exact author', () => {
    expect(matches('"JohnDoe"', mod())).toBe(true);
    expect(matches('"John"', mod())).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// matchesSearchQuery — field-specific
// ---------------------------------------------------------------------------

describe('field-specific search', () => {
  it('tag: restricts to tags only', () => {
    expect(matches('tag:hair', mod())).toBe(true);
    expect(matches('tag:john', mod())).toBe(false); // "john" is author, not tag
  });

  it('author: restricts to author only', () => {
    expect(matches('author:john', mod())).toBe(true);
    expect(matches('author:hair', mod())).toBe(false);
  });

  it('name: restricts to name only', () => {
    expect(matches('name:blue', mod())).toBe(true);
    expect(matches('name:john', mod())).toBe(false);
  });

  it.skip('id: restricts to id only', () => {
    expect(matches('id:abc', mod())).toBe(true);
    expect(matches('id:blue', mod())).toBe(false);
  });

  it('negated field prefix', () => {
    expect(matches('-tag:weapon', mod())).toBe(true);
    expect(matches('-tag:hair', mod())).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// matchesSearchQuery — complex scenarios
// ---------------------------------------------------------------------------

describe('complex queries', () => {
  const mods = {
    blueHair: mod(),
    redSkin: mod({
      id: 'def456',
      name: 'Red Skin Texture',
      author: 'JaneDoe',
      tags: ['skin', 'red', 'texture'],
    }),
    nsfwMod: mod({
      id: 'ghi789',
      name: 'NSFW Content',
      author: 'Anon',
      tags: ['nsfw', 'adult'],
    }),
  };

  it('tag:hair | tag:skin → matches blue hair and red skin, not nsfw', () => {
    expect(matches('tag:hair | tag:skin', mods.blueHair)).toBe(true);
    expect(matches('tag:hair | tag:skin', mods.redSkin)).toBe(true);
    expect(matches('tag:hair | tag:skin', mods.nsfwMod)).toBe(false);
  });

  it('hair -nsfw | skin -nsfw → excludes nsfw mod', () => {
    expect(matches('hair -nsfw | skin -nsfw', mods.blueHair)).toBe(true);
    expect(matches('hair -nsfw | skin -nsfw', mods.redSkin)).toBe(true);
    expect(matches('hair -nsfw | skin -nsfw', mods.nsfwMod)).toBe(false);
  });

  it('author:john tag:cosmetic → only blue hair mod', () => {
    expect(matches('author:john tag:cosmetic', mods.blueHair)).toBe(true);
    expect(matches('author:john tag:cosmetic', mods.redSkin)).toBe(false);
    expect(matches('author:john tag:cosmetic', mods.nsfwMod)).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// Edge cases
// ---------------------------------------------------------------------------

describe('edge cases', () => {
  it('handles record with no author', () => {
    expect(matches('john', mod({ author: undefined }))).toBe(false);
    expect(matches('author:john', mod({ author: undefined }))).toBe(false);
  });

  it('handles record with no tags', () => {
    expect(matches('tag:hair', mod({ tags: undefined }))).toBe(false);
    expect(matches('hair', mod({ tags: undefined }))).toBe(true); // matches name
  });

  it('handles lone pipe', () => {
    const q = parseSearchQuery('|');
    expect(q.isEmpty).toBe(true);
  });

  it.skip('handles lone dash', () => {
    // A lone "-" with nothing after is not a negation, just a dash
    // The tokenizer will skip it since there's no following char
    const q = parseSearchQuery('- ');
    expect(q.isEmpty).toBe(true);
  });

  it('handles multiple pipes in a row', () => {
    const q = parseSearchQuery('hair || skin');
    expect(q.groups).toHaveLength(2);
  });

  it('handles query with only spaces between pipes', () => {
    const q = parseSearchQuery(' | hair | ');
    expect(q.groups).toHaveLength(1);
    expect(q.groups[0].terms[0].text).toBe('hair');
  });
});

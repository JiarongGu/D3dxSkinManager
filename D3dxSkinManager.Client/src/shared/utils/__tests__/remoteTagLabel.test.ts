import { describe, it, expect } from 'vitest';
import { remoteTagLabel, remoteTagLabelsDeduped, mergeTagCountsByLabel } from '../remoteTagLabel';

// Two raw tags (A, B) map to the same display label C; X maps to Y — the "composition" case (#30).
const labels = { en: { A: 'C', B: 'C', X: 'Y' } };

describe('remoteTagLabel helpers', () => {
  it('remoteTagLabel maps through the label table, falls back to the raw tag', () => {
    expect(remoteTagLabel(labels, 'en', 'A')).toBe('C');
    expect(remoteTagLabel(labels, 'en', 'Z')).toBe('Z');
    expect(remoteTagLabel(undefined, 'en', 'A')).toBe('A');
  });

  it('remoteTagLabelsDeduped collapses tags sharing a label into one chip', () => {
    expect(remoteTagLabelsDeduped(labels, 'en', ['A', 'B', 'X'])).toEqual([
      { key: 'C', label: 'C' },
      { key: 'Y', label: 'Y' },
    ]);
  });

  it('remoteTagLabelsDeduped keeps distinct raw tags when there are no labels', () => {
    expect(remoteTagLabelsDeduped(undefined, 'en', ['A', 'B'])).toEqual([
      { key: 'A', label: 'A' },
      { key: 'B', label: 'B' },
    ]);
  });

  it('mergeTagCountsByLabel sums counts of same-label tags, first-appearance order', () => {
    expect(
      mergeTagCountsByLabel(
        [{ name: 'A', count: 2 }, { name: 'B', count: 3 }, { name: 'X', count: 1 }],
        labels,
        'en',
      ),
    ).toEqual([{ label: 'C', count: 5 }, { label: 'Y', count: 1 }]);
  });
});

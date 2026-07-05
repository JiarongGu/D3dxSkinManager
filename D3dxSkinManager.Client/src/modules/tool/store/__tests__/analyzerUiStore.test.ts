import { describe, it, expect, beforeEach } from 'vitest';
import { useAnalyzerUiStore } from '../analyzerUiStore';

const s = () => useAnalyzerUiStore.getState();

describe('analyzerUiStore', () => {
  beforeEach(() => {
    useAnalyzerUiStore.setState({
      profileId: undefined, viewMode: 'scan', sessionId: undefined, findingsFilter: 'all', searchText: '',
    });
  });

  it('persists view/session/filter/search across reads (survives tool unmount)', () => {
    s().ensureProfile('p1');
    s().setViewMode('findings');
    s().setSession('sess-1');
    s().setFindingsFilter('duplicates');
    s().setSearchText('安东');

    expect(s().viewMode).toBe('findings');
    expect(s().sessionId).toBe('sess-1');
    expect(s().findingsFilter).toBe('duplicates');
    expect(s().searchText).toBe('安东');
  });

  it('ensureProfile resets state when the profile changes, keeps it otherwise', () => {
    s().ensureProfile('p1');
    s().setViewMode('findings');
    s().setSession('sess-1');

    s().ensureProfile('p1'); // same profile — no reset
    expect(s().sessionId).toBe('sess-1');

    s().ensureProfile('p2'); // different profile — stale state is meaningless
    expect(s().viewMode).toBe('scan');
    expect(s().sessionId).toBeUndefined();
    expect(s().findingsFilter).toBe('all');
  });
});

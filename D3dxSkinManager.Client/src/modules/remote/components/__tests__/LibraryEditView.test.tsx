import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { RemoteLibrary, RemoteSourceInfo } from '../../../../shared/types/remote.types';

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (k: string) => k, i18n: { language: 'en' } }),
}));
vi.mock('../../../../shared/context/ProfileContext', () => ({
  useProfile: () => ({ selectedProfileId: 'p1' }),
}));

const indexTags = vi.fn();
const labelsGet = vi.fn();
const libraryUpdate = vi.fn();
const labelsSet = vi.fn();
vi.mock('../../../../shared/services/ipc', () => ({
  api: {
    remote: {
      indexTags: (...a: any[]) => indexTags(...a),
      labelsGet: (...a: any[]) => labelsGet(...a),
      libraryUpdate: (...a: any[]) => libraryUpdate(...a),
      labelsSet: (...a: any[]) => labelsSet(...a),
    },
  },
}));
vi.mock('../../../../shared/utils/errorHandler', () => ({ handleError: vi.fn() }));

import { LibraryEditView } from '../LibraryEditView';

const library: RemoteLibrary = {
  id: 'lib1',
  sourceId: 's1',
  listId: '1',
  name: 'My Lib',
  tagRules: [],
  paramValues: {},
  addedAtUtc: '2026-01-01T00:00:00Z',
};

const sources: RemoteSourceInfo[] = [
  { id: 's1', name: 'Site', baseUrl: 'https://a.example', lists: [{ id: '1', name: 'G' }], hasSearch: false, tagLabels: {}, params: [] },
];

describe('LibraryEditView', () => {
  beforeEach(() => {
    indexTags.mockReset().mockResolvedValue([]);
    labelsGet.mockReset().mockResolvedValue({});
    libraryUpdate.mockReset().mockResolvedValue(undefined);
    labelsSet.mockReset().mockResolvedValue(undefined);
  });

  it('disables Save until the library changes (dirty-gating)', async () => {
    render(<LibraryEditView library={library} sources={sources} categories={[]} onSaved={vi.fn()} onCancel={vi.fn()} />);
    await waitFor(() => expect(labelsGet).toHaveBeenCalled()); // mount IPC settled → baseline captured

    const save = screen.getByRole('button', { name: 'common.save' });
    expect(save).toBeDisabled();

    await userEvent.type(screen.getByDisplayValue('My Lib'), 'X'); // Detail tab active → name input present
    expect(save).not.toBeDisabled();
  });

  it('saves via libraryUpdate + labelsSet, then calls onSaved', async () => {
    const onSaved = vi.fn();
    render(<LibraryEditView library={library} sources={sources} categories={[]} onSaved={onSaved} onCancel={vi.fn()} />);
    await waitFor(() => expect(labelsGet).toHaveBeenCalled());

    await userEvent.type(screen.getByDisplayValue('My Lib'), 'X');
    await userEvent.click(screen.getByRole('button', { name: 'common.save' }));

    await waitFor(() => expect(onSaved).toHaveBeenCalled());
    expect(libraryUpdate).toHaveBeenCalledWith('p1', expect.objectContaining({ id: 'lib1', name: 'My LibX' }));
    expect(labelsSet).toHaveBeenCalledWith('p1', 's1', 'en', expect.anything());
  });

  it('loads the library index tags on mount (seeds the rule tag-picker)', async () => {
    render(<LibraryEditView library={library} sources={sources} categories={[]} onSaved={vi.fn()} onCancel={vi.fn()} />);
    await waitFor(() => expect(indexTags).toHaveBeenCalledWith('p1', 's1', '1'));
  });
});

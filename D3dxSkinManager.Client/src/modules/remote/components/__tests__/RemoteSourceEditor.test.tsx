import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { RemoteSourceConfig } from '../../../../shared/types/remote.types';

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (k: string) => k, i18n: { language: 'en' } }),
}));

vi.mock('../../../../shared/context/ProfileContext', () => ({
  useProfile: () => ({ selectedProfileId: 'p1' }),
}));

const testSource = vi.fn();
const saveSource = vi.fn();
const getSourceDefault = vi.fn();
vi.mock('../../../../shared/services/ipc', () => ({
  api: {
    remote: {
      testSource: (...a: any[]) => testSource(...a),
      saveSource: (...a: any[]) => saveSource(...a),
      getSourceDefault: (...a: any[]) => getSourceDefault(...a),
    },
  },
}));

vi.mock('../../../../shared/utils/errorHandler', () => ({ handleError: vi.fn() }));
vi.mock('../../../../shared/utils/notification', () => ({ notification: { success: vi.fn(), info: vi.fn() } }));

import { RemoteSourceEditor } from '../RemoteSourceEditor';

const initial: RemoteSourceConfig = {
  id: 's1',
  name: 'Site',
  baseUrl: 'https://a.example',
  engine: 'http',
  fetcher: 'http',
  lists: [{ id: '1', name: 'G' }],
  listUrlFirstPage: '/?l_{list}/',
  cardPattern: 'c',
  detailTitlePattern: 'd',
  downloadLinkPattern: 'x',
  resolvers: [],
};

describe('RemoteSourceEditor', () => {
  beforeEach(() => {
    testSource.mockReset();
    saveSource.mockReset();
    getSourceDefault.mockReset();
  });

  it('disables Save until something changes (dirty-tracking)', async () => {
    render(<RemoteSourceEditor initial={initial} origin="customized" onCancel={vi.fn()} onSaved={vi.fn()} />);

    const save = screen.getByRole('button', { name: 'remote.saveSource' });
    expect(save).toBeDisabled();

    await userEvent.type(screen.getByPlaceholderText('My Site'), 'X');
    expect(save).toBeEnabled();
  });

  it('runs Test connection and renders the result indicator', async () => {
    testSource.mockResolvedValue({
      success: true,
      cardCount: 2,
      sampleTitles: ['a'],
      totalPages: 3,
      detailFetched: true,
      detailTitle: 't',
      detailDownloads: [],
      detailImageCount: 0,
    });

    render(<RemoteSourceEditor initial={initial} origin="customized" onCancel={vi.fn()} onSaved={vi.fn()} />);
    // Buttons carry an icon → antd prefixes the icon aria-label to the accessible name, so match by text.
    await userEvent.click(screen.getByText('remote.testSource'));

    await waitFor(() => expect(testSource).toHaveBeenCalledTimes(1));
    expect(await screen.findByTestId('remote-test-result')).toBeInTheDocument();
    expect(screen.getByText('remote.testConnected')).toBeInTheDocument();
  });

  it('offers "Compare with default" only for a customized source', () => {
    const { rerender } = render(
      <RemoteSourceEditor initial={initial} origin="customized" onCancel={vi.fn()} onSaved={vi.fn()} />,
    );
    expect(screen.getByText('remote.compareWithDefault')).toBeInTheDocument();

    rerender(<RemoteSourceEditor initial={initial} origin="custom" onCancel={vi.fn()} onSaved={vi.fn()} />);
    expect(screen.queryByText('remote.compareWithDefault')).not.toBeInTheDocument();
  });
});

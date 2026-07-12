import React from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { RemoteSourceConfig } from '../../../../shared/types/remote.types';

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (k: string) => k, i18n: { language: 'en' } }),
}));

import { RemoteSourceCompareDialog } from '../RemoteSourceCompareDialog';

const makeCfg = (over: Partial<RemoteSourceConfig> = {}): RemoteSourceConfig => ({
  id: 's1',
  name: 'Site',
  baseUrl: 'https://a.example',
  engine: 'http',
  fetcher: 'http',
  lists: [{ id: '1', name: 'G' }],
  listUrlFirstPage: '/?l_{list}/',
  listUrlTemplate: '',
  searchUrlTemplate: '',
  cardPattern: 'c',
  cardScopePattern: '',
  totalPagesPattern: '',
  detailTitlePattern: 'd',
  detailImagePattern: '',
  downloadLinkPattern: 'x',
  entryIdPattern: '',
  imageDatePattern: '',
  titleTagPattern: '',
  resolvers: [],
  ...over,
});

describe('RemoteSourceCompareDialog', () => {
  it('lists ONLY the fields that differ from default', () => {
    const current = makeCfg({ baseUrl: 'https://mirror.example' });
    const def = makeCfg({ baseUrl: 'https://a.example' });
    render(<RemoteSourceCompareDialog visible current={current} def={def} onRevert={vi.fn()} onCancel={vi.fn()} />);

    expect(screen.getByTestId('compare-row-baseUrl')).toBeInTheDocument();
    expect(screen.queryByTestId('compare-row-name')).not.toBeInTheDocument();
  });

  it('reverts a selected field to its default value', async () => {
    const onRevert = vi.fn();
    const current = makeCfg({ baseUrl: 'https://mirror.example', name: 'Custom' });
    const def = makeCfg({ baseUrl: 'https://a.example', name: 'Custom' }); // only baseUrl differs
    render(<RemoteSourceCompareDialog visible current={current} def={def} onRevert={onRevert} onCancel={vi.fn()} />);

    // Checkboxes: [0] = select-all header, [1] = the baseUrl row.
    await userEvent.click(screen.getAllByRole('checkbox')[1]);
    await userEvent.click(screen.getByText('remote.compareRevert'));

    expect(onRevert).toHaveBeenCalledTimes(1);
    const reverted = onRevert.mock.calls[0][0] as RemoteSourceConfig;
    expect(reverted.baseUrl).toBe('https://a.example'); // reverted to default
    expect(reverted.name).toBe('Custom'); // untouched
  });

  it('"Take all" reverts every differing field without ticking any', async () => {
    const onRevert = vi.fn();
    const current = makeCfg({ baseUrl: 'https://mirror.example', cardPattern: 'mine' });
    const def = makeCfg({ baseUrl: 'https://a.example', cardPattern: 'shipped' }); // two fields differ
    render(<RemoteSourceCompareDialog visible current={current} def={def} onRevert={onRevert} onCancel={vi.fn()} />);

    await userEvent.click(screen.getByText('remote.compareTakeAll'));

    const reverted = onRevert.mock.calls[0][0] as RemoteSourceConfig;
    expect(reverted.baseUrl).toBe('https://a.example');
    expect(reverted.cardPattern).toBe('shipped');
  });

  it('shows the no-changes message when identical to default', () => {
    const cfg = makeCfg();
    render(<RemoteSourceCompareDialog visible current={cfg} def={makeCfg()} onRevert={vi.fn()} onCancel={vi.fn()} />);
    expect(screen.getByText('remote.compareNoChanges')).toBeInTheDocument();
  });

  it('does NOT treat empty-string vs missing/undefined as a change', () => {
    const current = makeCfg({ searchUrlTemplate: '' }); // current carries an empty string
    const def = makeCfg();
    delete (def as Record<string, unknown>).searchUrlTemplate; // res default omits the null field
    render(<RemoteSourceCompareDialog visible current={current} def={def} onRevert={vi.fn()} onCancel={vi.fn()} />);

    expect(screen.queryByTestId('compare-row-searchUrlTemplate')).not.toBeInTheDocument();
    expect(screen.getByText('remote.compareNoChanges')).toBeInTheDocument();
  });
});

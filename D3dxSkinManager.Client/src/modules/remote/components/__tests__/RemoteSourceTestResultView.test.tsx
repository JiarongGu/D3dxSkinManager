import React from 'react';
import { render, screen } from '@testing-library/react';
import type { RemoteSourceTestResult } from '../../../../shared/types/remote.types';

// i18n: echo keys; append the count so per-count labels are assertable.
vi.mock('react-i18next', () => ({
  useTranslation: () => ({
    t: (k: string, o?: { count?: number }) => (o && o.count != null ? `${k}:${o.count}` : k),
    i18n: { language: 'en' },
  }),
}));

import { RemoteSourceTestResultView } from '../RemoteSourceTestResultView';

const ok = (over: Partial<RemoteSourceTestResult> = {}): RemoteSourceTestResult => ({
  success: true,
  cardCount: 2,
  sampleTitles: ['Alpha', 'Beta'],
  totalPages: 5,
  detailFetched: true,
  detailTitle: 'Alpha',
  detailDownloads: [{ name: 'd', url: 'u', type: 'direct' }],
  detailImageCount: 3,
  ...over,
});

describe('RemoteSourceTestResultView', () => {
  it('shows the testing state', () => {
    render(<RemoteSourceTestResultView testing />);
    expect(screen.getByText('remote.testTesting')).toBeInTheDocument();
  });

  it('renders per-check status on success', () => {
    render(<RemoteSourceTestResultView result={ok()} />);
    expect(screen.getByText('remote.testConnected')).toBeInTheDocument();
    expect(screen.getByText('remote.testCards:2')).toBeInTheDocument();
    expect(screen.getByText('remote.testDetailOk')).toBeInTheDocument();
    expect(screen.getByText('remote.testDownloads:1')).toBeInTheDocument();
    expect(screen.getByText('Alpha · Beta')).toBeInTheDocument();
  });

  it('warns when no cards matched (and shows no detail checks)', () => {
    render(<RemoteSourceTestResultView result={ok({ cardCount: 0, detailFetched: false, detailTitle: undefined, detailDownloads: [], detailImageCount: 0 })} />);
    expect(screen.getByText('remote.testNoCards')).toBeInTheDocument();
    expect(screen.queryByText('remote.testDetailOk')).not.toBeInTheDocument();
    expect(screen.queryByText('remote.testDownloads:0')).not.toBeInTheDocument();
  });

  it('renders a failure with the backend error message', () => {
    render(<RemoteSourceTestResultView result={ok({ success: false, error: 'HTTP 500 upstream' })} />);
    expect(screen.getByText('remote.testFailed')).toBeInTheDocument();
    expect(screen.getByText('HTTP 500 upstream')).toBeInTheDocument();
  });

  it('renders a client-side error (invalid config) as a failure', () => {
    render(<RemoteSourceTestResultView error="Bad JSON at line 3" />);
    expect(screen.getByText('remote.testFailed')).toBeInTheDocument();
    expect(screen.getByText('Bad JSON at line 3')).toBeInTheDocument();
  });
});

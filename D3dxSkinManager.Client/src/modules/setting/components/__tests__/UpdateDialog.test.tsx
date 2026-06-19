import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { UpdateInfo } from '../../../../shared/services/ipc';

// i18n: echo keys.
vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

vi.mock('antd', () => ({
  Spin: () => <div data-testid="spin" />,
}));

// FormDialog stub: render title + children + footer when visible.
vi.mock('../../../../shared/components/dialogs/FormDialog', () => ({
  FormDialog: ({ visible, title, children, footer }: any) =>
    visible ? (
      <div>
        <div>{title}</div>
        {children}
        <div>{footer}</div>
      </div>
    ) : null,
}));

vi.mock('../../../../shared/components/compact', () => ({
  CompactButton: ({ children, onClick }: any) => <button onClick={onClick}>{children}</button>,
  CompactSpace: ({ children }: any) => <div>{children}</div>,
}));

const checkForUpdate = vi.fn();
const downloadUpdate = vi.fn();
const getUpdateState = vi.fn();
vi.mock('../../../../shared/services/ipc', () => ({
  systemService: {
    checkForUpdate: (...a: any[]) => checkForUpdate(...a),
    downloadUpdate: (...a: any[]) => downloadUpdate(...a),
    getUpdateState: (...a: any[]) => getUpdateState(...a),
  },
}));

vi.mock('../../../../shared/utils/errorHandler', () => ({ handleError: vi.fn() }));
vi.mock('../../../../shared/utils/logger', () => ({ logger: { error: vi.fn(), warn: vi.fn() } }));
vi.mock('../../../../shared/utils/formatBytes', () => ({ formatBytes: (n: number) => `${n}B` }));

import { UpdateDialog } from '../UpdateDialog';

const baseInfo: UpdateInfo = {
  currentVersion: '2.4',
  latestVersion: '2.5',
  updateAvailable: true,
  releaseName: 'v2.5',
  releaseNotes: 'notes',
  releaseUrl: 'https://example.com/r/v2.5',
  publishedAt: '2026-06-19T00:00:00Z',
  hasManifest: false,
  changedFileCount: 0,
  downloadSize: 0,
};

describe('UpdateDialog', () => {
  beforeEach(() => {
    checkForUpdate.mockReset();
    downloadUpdate.mockReset();
    getUpdateState.mockReset();
    getUpdateState.mockResolvedValue({ pending: false, pendingVersion: '' });
  });

  it('runs the check on open and shows the up-to-date state', async () => {
    checkForUpdate.mockResolvedValue({ ...baseInfo, updateAvailable: false });
    render(<UpdateDialog open onClose={() => {}} />);

    await waitFor(() => expect(checkForUpdate).toHaveBeenCalledTimes(1));
    expect(await screen.findByText('update.upToDate.title')).toBeInTheDocument();
  });

  it('shows the available state with a Download action', async () => {
    checkForUpdate.mockResolvedValue(baseInfo);
    render(<UpdateDialog open onClose={() => {}} />);

    expect(await screen.findByText('update.available.title')).toBeInTheDocument();
    expect(screen.getByText('update.download')).toBeInTheDocument();
  });

  it('downloads + stages on Download and flips to the ready state', async () => {
    checkForUpdate.mockResolvedValue(baseInfo);
    downloadUpdate.mockResolvedValue({ started: true });
    render(<UpdateDialog open onClose={() => {}} />);

    await screen.findByText('update.download');
    // After download starts, the next getUpdateState poll reports a staged update.
    getUpdateState.mockResolvedValue({ pending: true, pendingVersion: '2.5' });
    await userEvent.click(screen.getByText('update.download'));

    expect(downloadUpdate).toHaveBeenCalledTimes(1);
    expect(await screen.findByText('update.ready.title', {}, { timeout: 4000 })).toBeInTheDocument();
  });

  it('shows the ready state directly when an update is already staged', async () => {
    getUpdateState.mockResolvedValue({ pending: true, pendingVersion: '2.5' });
    render(<UpdateDialog open onClose={() => {}} />);

    expect(await screen.findByText('update.ready.title')).toBeInTheDocument();
    expect(checkForUpdate).not.toHaveBeenCalled();
  });

  it('skips the network check when prefetched info is supplied', async () => {
    render(<UpdateDialog open onClose={() => {}} prefetched={baseInfo} />);

    expect(await screen.findByText('update.available.title')).toBeInTheDocument();
    expect(checkForUpdate).not.toHaveBeenCalled();
  });

  it('shows the failure state when the check throws', async () => {
    checkForUpdate.mockRejectedValue(new Error('offline'));
    render(<UpdateDialog open onClose={() => {}} />);

    expect(await screen.findByText('update.checkFailed')).toBeInTheDocument();
  });
});

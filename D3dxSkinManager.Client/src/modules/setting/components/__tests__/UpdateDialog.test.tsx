import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { UpdateInfo } from '../../../../shared/services/ipc';

// i18n: echo keys (with the version param appended where relevant so assertions stay meaningful).
vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

// antd Spin → simple marker.
vi.mock('antd', () => ({
  Spin: () => <div data-testid="spin" />,
}));

// FormDialog stub: render title + children + footer when visible (footer holds the action buttons).
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
const openUrl = vi.fn();
vi.mock('../../../../shared/services/ipc', () => ({
  systemService: {
    checkForUpdate: (...args: any[]) => checkForUpdate(...args),
    openUrl: (...args: any[]) => openUrl(...args),
  },
}));

vi.mock('../../../../shared/utils/errorHandler', () => ({ handleError: vi.fn() }));
vi.mock('../../../../shared/utils/logger', () => ({ logger: { error: vi.fn(), warn: vi.fn() } }));

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
    openUrl.mockReset();
  });

  it('runs the check on open and shows the up-to-date state', async () => {
    checkForUpdate.mockResolvedValue({ ...baseInfo, updateAvailable: false });
    render(<UpdateDialog open onClose={() => {}} />);

    await waitFor(() => expect(checkForUpdate).toHaveBeenCalledTimes(1));
    expect(await screen.findByText('update.upToDate.title')).toBeInTheDocument();
    expect(screen.getByText('update.close')).toBeInTheDocument();
  });

  it('shows the available state and opens the release URL on download', async () => {
    checkForUpdate.mockResolvedValue(baseInfo);
    const onClose = vi.fn();
    render(<UpdateDialog open onClose={onClose} />);

    expect(await screen.findByText('update.available.title')).toBeInTheDocument();

    await userEvent.click(screen.getByText('update.download'));
    expect(openUrl).toHaveBeenCalledWith('https://example.com/r/v2.5');
    await waitFor(() => expect(onClose).toHaveBeenCalled());
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

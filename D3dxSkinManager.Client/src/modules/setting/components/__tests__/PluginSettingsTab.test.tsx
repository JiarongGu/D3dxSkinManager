import React from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// i18n: echo keys (interpolation args ignored — assert by key).
vi.mock('react-i18next', () => ({ useTranslation: () => ({ t: (k: string) => k }) }));

vi.mock('antd', () => ({ Progress: () => <div data-testid="progress" /> }));

vi.mock('../../../../shared/components/compact', () => ({
  CompactCard: ({ children, title, extra }: any) => <div>{title}{extra}{children}</div>,
  CompactButton: ({ children, onClick, disabled }: any) => (
    <button onClick={onClick} disabled={disabled}>{children}</button>
  ),
  CompactSwitch: ({ checkedChildren }: any) => <div>{checkedChildren}</div>,
}));

vi.mock('../../../../shared/components/common/StatusTag', () => ({
  StatusTag: ({ label }: any) => <span>{label}</span>,
}));

const getAll = vi.fn();
const getDirectory = vi.fn();
const getAvailablePacks = vi.fn();
const getLoadFailures = vi.fn();
const getPendingUpdates = vi.fn();
const checkUpdates = vi.fn();
const downloadPack = vi.fn();
const restartForUpdate = vi.fn();
vi.mock('../../../../shared/services/ipc', () => ({
  pluginService: {
    getAll: (...a: any[]) => getAll(...a),
    getDirectory: (...a: any[]) => getDirectory(...a),
    getAvailablePacks: (...a: any[]) => getAvailablePacks(...a),
    getLoadFailures: (...a: any[]) => getLoadFailures(...a),
    getPendingUpdates: (...a: any[]) => getPendingUpdates(...a),
    checkUpdates: (...a: any[]) => checkUpdates(...a),
    downloadPack: (...a: any[]) => downloadPack(...a),
  },
  systemService: { restartForUpdate: (...a: any[]) => restartForUpdate(...a), openDirectory: vi.fn() },
}));

vi.mock('../../../../shared/context/ProfileContext', () => ({ useProfile: () => ({ selectedProfileId: 'p1' }) }));
// processStore selector — no running/completed processes.
vi.mock('../../../../shared/store/processStore', () => ({ useProcessStore: (sel: any) => sel({ processes: [] }) }));
vi.mock('../../../../shared/utils/errorHandler', () => ({ handleError: vi.fn() }));
vi.mock('../../../../shared/utils/notification', () => ({ notification: { info: vi.fn() } }));

import { PluginSettingsTab } from '../PluginSettingsTab';

describe('PluginSettingsTab — load failures + pending-restart', () => {
  beforeEach(() => {
    for (const m of [getAll, getDirectory, getAvailablePacks, getLoadFailures, getPendingUpdates, checkUpdates, downloadPack, restartForUpdate]) m.mockReset();
    getAll.mockResolvedValue([]);
    getDirectory.mockResolvedValue({ path: 'X:/plugins' });
    getAvailablePacks.mockResolvedValue([]);
    getLoadFailures.mockResolvedValue([]);
    getPendingUpdates.mockResolvedValue([]);
    checkUpdates.mockResolvedValue([]);
  });

  it('surfaces a failed pack with a Download update action when a compatible build exists', async () => {
    getLoadFailures.mockResolvedValue([
      { packId: 'content-veil-ai', dllName: 'cv.dll', reason: 'Core contract mismatch',
        name: 'Content Veil AI', updateAvailable: true, availableVersion: '2.0' },
    ]);
    render(<PluginSettingsTab />);

    expect(await screen.findByText('settings.plugins.failedSection')).toBeInTheDocument();
    expect(screen.getByText('Content Veil AI')).toBeInTheDocument();
    expect(screen.getByText('Core contract mismatch')).toBeInTheDocument();
    expect(screen.getByText('settings.plugins.loadFailed.download')).toBeInTheDocument();
  });

  it('offers no download (and shows the no-update hint) for a failed pack with no compatible build', async () => {
    getLoadFailures.mockResolvedValue([
      { packId: 'old-pack', dllName: 'x.dll', reason: 'mismatch', updateAvailable: false },
    ]);
    render(<PluginSettingsTab />);

    await screen.findByText('settings.plugins.failedSection');
    expect(screen.getByText('old-pack')).toBeInTheDocument(); // no catalog name → falls back to packId
    expect(screen.getByText('settings.plugins.loadFailed.noUpdate')).toBeInTheDocument();
    expect(screen.queryByText('settings.plugins.loadFailed.download')).not.toBeInTheDocument();
  });

  it('shows the pending-restart banner and restarts the app on click', async () => {
    getPendingUpdates.mockResolvedValue(['content-veil-ai']);
    restartForUpdate.mockResolvedValue({ restarting: true });
    render(<PluginSettingsTab />);

    const restartBtn = await screen.findByText('update.ready.restartNow');
    expect(screen.getByText('settings.plugins.pending.banner')).toBeInTheDocument();

    await userEvent.click(restartBtn);
    expect(restartForUpdate).toHaveBeenCalledTimes(1);
  });

  it('renders no failure UI when everything loaded cleanly', async () => {
    render(<PluginSettingsTab />);

    await screen.findByText('settings.plugins.loadedSection');
    expect(screen.queryByText('settings.plugins.failedSection')).not.toBeInTheDocument();
    expect(screen.queryByText('settings.plugins.pending.banner')).not.toBeInTheDocument();
  });
});

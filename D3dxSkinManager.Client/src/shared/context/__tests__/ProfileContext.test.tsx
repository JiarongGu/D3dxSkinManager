import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

const getAllProfiles = vi.fn();
const getProfileById = vi.fn();
const createProfile = vi.fn();
const updateProfile = vi.fn();
const deleteProfile = vi.fn();
const switchProfile = vi.fn();

vi.mock('../../services/ipc', () => ({
  profileService: {
    getAllProfiles: (...a: any[]) => getAllProfiles(...a),
    getProfileById: (...a: any[]) => getProfileById(...a),
    createProfile: (...a: any[]) => createProfile(...a),
    updateProfile: (...a: any[]) => updateProfile(...a),
    deleteProfile: (...a: any[]) => deleteProfile(...a),
    switchProfile: (...a: any[]) => switchProfile(...a),
  },
}));

import { ProfileProvider, useProfile } from '../ProfileContext';

const prof = (id: string, name = id) => ({ id, name }) as any;

const Harness: React.FC = () => {
  const { selectedProfileId, profiles, error, actions } = useProfile();
  return (
    <div>
      <div data-testid="selected">{selectedProfileId ?? 'none'}</div>
      <div data-testid="count">{profiles.length}</div>
      <div data-testid="error">{error ?? ''}</div>
      <button onClick={() => actions.createProfile('New').catch(() => {})}>create</button>
      <button onClick={() => actions.deleteProfile(selectedProfileId!).catch(() => {})}>del-selected</button>
    </div>
  );
};

const renderProvider = () =>
  render(
    <ProfileProvider>
      <Harness />
    </ProfileProvider>,
  );

describe('ProfileContext', () => {
  beforeEach(() => {
    getAllProfiles.mockReset();
    getProfileById.mockReset();
    createProfile.mockReset();
    deleteProfile.mockReset();
    switchProfile.mockReset();
    switchProfile.mockResolvedValue({ success: true });
  });

  it('loads profiles on mount and selects the active one', async () => {
    getAllProfiles.mockResolvedValue({ profiles: [prof('a'), prof('b')], activeProfileId: 'b' });

    renderProvider();

    await waitFor(() => expect(screen.getByTestId('selected')).toHaveTextContent('b'));
    expect(screen.getByTestId('count')).toHaveTextContent('2');
  });

  it('falls back to the first profile when the active id is not found', async () => {
    getAllProfiles.mockResolvedValue({ profiles: [prof('a'), prof('b')], activeProfileId: 'zzz' });

    renderProvider();

    await waitFor(() => expect(screen.getByTestId('selected')).toHaveTextContent('a'));
  });

  it('sets an error when initial load fails', async () => {
    getAllProfiles.mockRejectedValue(new Error('boom'));

    renderProvider();

    await waitFor(() =>
      expect(screen.getByTestId('error')).toHaveTextContent('Failed to initialize profiles'),
    );
  });

  it('createProfile adds the new profile to the list', async () => {
    getAllProfiles.mockResolvedValue({ profiles: [prof('a')], activeProfileId: 'a' });
    createProfile.mockResolvedValue(prof('c', 'New'));

    renderProvider();
    await waitFor(() => expect(screen.getByTestId('count')).toHaveTextContent('1'));

    await userEvent.click(screen.getByText('create'));

    await waitFor(() => expect(screen.getByTestId('count')).toHaveTextContent('2'));
    expect(createProfile).toHaveBeenCalledWith({ name: 'New', description: undefined });
  });

  it('refuses to delete the currently selected profile', async () => {
    getAllProfiles.mockResolvedValue({ profiles: [prof('a'), prof('b')], activeProfileId: 'a' });

    renderProvider();
    await waitFor(() => expect(screen.getByTestId('selected')).toHaveTextContent('a'));

    await userEvent.click(screen.getByText('del-selected'));

    // The guard blocks the delete (service never called) and surfaces an error.
    await waitFor(() => expect(screen.getByTestId('error')).toHaveTextContent('Failed to delete profile'));
    expect(deleteProfile).not.toHaveBeenCalled();
  });

  it('useProfile throws outside a provider', () => {
    const spy = vi.spyOn(console, 'error').mockImplementation(() => {});
    expect(() => render(<Harness />)).toThrow(/useProfile must be used within ProfileProvider/);
    spy.mockRestore();
  });
});

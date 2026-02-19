import { useState, useEffect, useCallback } from 'react';
import { message } from 'antd';
import { modService } from '../../mods/services/modService';
import { ModInfo } from '../../../shared/types/mod.types';
import { useProfile } from '../../../shared/context/ProfileContext';

export const useModData = () => {
  const [mods, setMods] = useState<ModInfo[]>([]);
  const [loading, setLoading] = useState(false);
  const [objects, setObjects] = useState<string[]>([]);
  const [authors, setAuthors] = useState<string[]>([]);
  const { state: profileState } = useProfile();

  const loadMods = useCallback(async () => {
    // Only load if we have a selected profile
    if (!profileState.selectedProfile) {
      console.log('[useModData] No profile selected, skipping mod load');
      setMods([]);
      return;
    }

    try {
      setLoading(true);
      const data = await modService.getAllMods(profileState.selectedProfile.id);
      setMods(data);
    } catch (error: unknown) {
      // Don't show error if it's just because no profile is selected
      const errorMessage = error instanceof Error ? error.message : 'Unknown error';
      if (!errorMessage.includes('Profile ID is required')) {
        message.error('Failed to load mods: ' + errorMessage);
      }
    } finally {
      setLoading(false);
    }
  }, [profileState.selectedProfile]);

  const loadFilters = useCallback(async () => {
    // Only load if we have a selected profile
    if (!profileState.selectedProfile) {
      console.log('[useModData] No profile selected, skipping filter load');
      setObjects([]);
      setAuthors([]);
      return;
    }

    try {
      const [objectsData, authorsData] = await Promise.all([
        modService.getObjectNames(profileState.selectedProfile.id),
        modService.getAuthors(profileState.selectedProfile.id)
      ]);
      setObjects(objectsData);
      setAuthors(authorsData);
    } catch (error: unknown) {
      // Don't log error if it's just because no profile is selected
      const errorMessage = error instanceof Error ? error.message : '';
      if (!errorMessage.includes('Profile ID is required')) {
        console.error('Failed to load filters:', error);
      }
    }
  }, [profileState.selectedProfile]);

  useEffect(() => {
    loadMods();
    loadFilters();
  }, [loadMods, loadFilters]);

  return {
    mods,
    loading,
    objects,
    authors,
    loadMods,
    loadFilters
  };
};

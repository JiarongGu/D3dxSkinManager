import { useCallback } from 'react';
import { message, Modal } from 'antd';
import { modService } from '../../mods/services/modService';

export const useModActions = (profileId: string, onModsChange: () => Promise<void>) => {
  const handleLoadMod = useCallback(async (sha: string) => {
    try {
      await modService.loadMod(profileId,sha);
      message.success('Mod loaded successfully');
      await onModsChange();
    } catch (error) {
      message.error('Failed to load mod: ' + (error as Error).message);
    }
  }, [profileId, onModsChange]);

  const handleUnloadMod = useCallback(async (sha: string) => {
    try {
      await modService.unloadMod(profileId, sha);
      message.success('Mod unloaded successfully');
      await onModsChange();
    } catch (error) {
      message.error('Failed to unload mod: ' + (error as Error).message);
    }
  }, [profileId, onModsChange]);

  const handleDeleteMod = useCallback(async (sha: string, name: string, onFiltersChange?: () => Promise<void>) => {
    Modal.confirm({
      title: 'Delete Mod',
      content: `Are you sure you want to permanently delete "${name}"? This action cannot be undone.`,
      okText: 'Delete',
      okType: 'danger',
      cancelText: 'Cancel',
      onOk: async () => {
        try {
          await modService.deleteMod(profileId, sha);
          message.success('Mod deleted successfully');
          await onModsChange();
          if (onFiltersChange) {
            await onFiltersChange();
          }
        } catch (error) {
          message.error('Failed to delete mod: ' + (error as Error).message);
        }
      }
    });
  }, [profileId, onModsChange]);

  return {
    handleLoadMod,
    handleUnloadMod,
    handleDeleteMod
  };
};

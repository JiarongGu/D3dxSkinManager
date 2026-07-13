import { useTranslation } from 'react-i18next';
import { ModInfo } from '../../../shared/types/mod.types';
import { modService, systemService } from '../../../shared/services/ipc';
import { notification } from '../../../shared/utils/notification';
import { useProfile } from '../../../shared/context/ProfileContext';
import { useModsStore } from '../store/modsStore';
import { useModsState } from './useMods';
import { refreshMods } from '../operations/modOperations';

export interface ModListActions {
  /** Delete a mod's extracted cache (loaded `{id}/` or unloaded `DISABLED-{id}/`); no confirm. */
  deleteCachedMod: (mod: ModInfo) => Promise<void>;
  /** Re-compress a mod's cache folder back into its archive (shows a per-mod busy spinner). */
  updateArchive: (mod: ModInfo) => Promise<void>;
  /** Replace a mod's content from a picked archive (same id, metadata kept). */
  updateMod: (mod: ModInfo) => Promise<void>;
}

/**
 * The per-mod backend action handlers behind the mod-list right-click menu (delete-cache, update-archive,
 * replace-content). Each acks with a notification + refreshes / marks-busy as needed. Extracted verbatim
 * from ModList (behavior-preserving); every dependency is module-global so the hook takes no params. The
 * delete-CONFIRM flow stays in the component — it's coupled to the confirm-dialog state (see
 * oversized-file-splits.md: don't extract the entangled bits).
 */
export function useModListActions(): ModListActions {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();
  const busyModIds = useModsState((s) => s.busyModIds);

  const deleteCachedMod = async (mod: ModInfo) => {
    if (!selectedProfileId) {
      notification.error(t("errors.noProfileSelected"));
      return;
    }
    const profileId = selectedProfileId;
    try {
      const success = await modService.deleteCache(profileId, mod.id);
      if (success) {
        notification.success(t("mods.notifications.cacheDeleted", { name: mod.name }));
        // Refresh from backend to update hasCache and other properties.
        await refreshMods(profileId);
      } else {
        notification.error(t("mods.notifications.deleteCacheFailed"));
      }
    } catch (error: unknown) {
      const errorMessage = error instanceof Error ? error.message : "Unknown error";
      notification.error(`${t("mods.notifications.deleteCacheFailed")}: ${errorMessage}`);
    }
  };

  const updateArchive = async (mod: ModInfo) => {
    if (!selectedProfileId || busyModIds.has(mod.id)) return;
    const { addBusyMod, removeBusyMod } = useModsStore.getState();
    addBusyMod(mod.id);
    try {
      await modService.updateArchiveFromCache(selectedProfileId, mod.id);
      notification.success(t("mods.notifications.archiveUpdated", { name: mod.name }));
    } catch (error: unknown) {
      notification.error(t("mods.notifications.archiveUpdateFailed"));
    } finally {
      removeBusyMod(mod.id);
    }
  };

  const updateMod = async (mod: ModInfo) => {
    if (!selectedProfileId || busyModIds.has(mod.id)) return;
    const res = await systemService.openFileDialog({
      title: t("contextMenu.updateMod"),
      filters: [{ name: t("mods.modArchiveFilter"), extensions: ["7z", "zip", "rar"] }],
      rememberPathKey: "modUpdateSource",
    });
    if (!res.success || !res.filePath) return;

    const { addBusyMod, removeBusyMod } = useModsStore.getState();
    addBusyMod(mod.id);
    try {
      await modService.updateMod(selectedProfileId, mod.id, res.filePath);
      notification.success(t("mods.notifications.modUpdated", { name: mod.name }));
      await refreshMods(selectedProfileId);
    } catch (error: unknown) {
      notification.error(t("mods.notifications.modUpdateFailed"));
    } finally {
      removeBusyMod(mod.id);
    }
  };

  return { deleteCachedMod, updateArchive, updateMod };
}

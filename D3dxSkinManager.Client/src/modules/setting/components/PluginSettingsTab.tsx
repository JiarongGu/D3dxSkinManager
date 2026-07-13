import React, { useCallback, useEffect, useState } from "react";
import { Progress } from "antd";
import {
  ApiOutlined,
  CloudDownloadOutlined,
  ExclamationCircleOutlined,
  EyeInvisibleOutlined,
  FolderOpenOutlined,
  ReloadOutlined,
} from "@ant-design/icons";
import { CompactCard, CompactButton, CompactSwitch } from "../../../shared/components/compact";
import { StatusTag } from "../../../shared/components/common/StatusTag";
import { useTranslation } from "react-i18next";
import { useProfile } from "../../../shared/context/ProfileContext";
import { pluginService, systemService, PluginInfo, PluginUpdateInfo, PluginPackInfo, PluginLoadFailure } from "../../../shared/services/ipc";
import { useProcessStore } from "../../../shared/store/processStore";
import { handleError } from "../../../shared/utils/errorHandler";
import { notification } from "../../../shared/utils/notification";
import "./PluginSettingsTab.css";

/** Icon per capability — a plugin's role is legible at a glance. */
const CAPABILITY_ICON: Record<string, React.ReactNode> = {
  ImageReview: <EyeInvisibleOutlined />,
};

/**
 * Plugin management: cards for the plugins loaded in the current profile (generic plugin system —
 * dll packs in {profile}/plugins/) with enable toggles, plus an "available" section that
 * downloads official packs from GitHub with live progress. Loaded packs take effect immediately;
 * removals need a restart (assemblies can't unload).
 */
export const PluginSettingsTab: React.FC = () => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();

  const [plugins, setPlugins] = useState<PluginInfo[]>();
  const [directory, setDirectory] = useState<string>();
  const [busyId, setBusyId] = useState<string>();
  // pluginId → update status (only installed official packs; empty when offline / up to date).
  const [updates, setUpdates] = useState<Record<string, PluginUpdateInfo>>({});
  // Available official packs — pulled from the plugin repo manifest (no hard-coded list); [] when offline.
  const [availablePacks, setAvailablePacks] = useState<PluginPackInfo[]>([]);
  // Installed packs that FAILED to load (contract mismatch after an app update, …) — surfaced so the user
  // can download a compatible build. Not in `plugins` (they never registered).
  const [loadFailures, setLoadFailures] = useState<PluginLoadFailure[]>([]);
  // Pack ids whose update is staged in .pending, awaiting a restart to apply (mirrors app-update pending).
  const [pendingUpdates, setPendingUpdates] = useState<string[]>([]);

  const load = useCallback(async () => {
    if (!selectedProfileId) return;
    try {
      const [list, dir] = await Promise.all([
        pluginService.getAll(selectedProfileId),
        pluginService.getDirectory(selectedProfileId),
      ]);
      setPlugins(Array.isArray(list) ? list : []);
      setDirectory(dir?.path);
    } catch (error) {
      handleError(error);
      setPlugins([]);
    }
    // The available-pack catalog is a BACKGROUND, network-tolerant fetch — it must never block the
    // installed list or surface an error when offline (empty catalog just shows nothing to install).
    try {
      setAvailablePacks(await pluginService.getAvailablePacks(selectedProfileId));
    } catch {
      setAvailablePacks([]);
    }
    // Load failures (needs catalog to enrich — network-tolerant) + staged-update ids (local dir scan).
    try {
      const [failures, pending] = await Promise.all([
        pluginService.getLoadFailures(selectedProfileId),
        pluginService.getPendingUpdates(selectedProfileId),
      ]);
      setLoadFailures(failures);
      setPendingUpdates(pending);
    } catch {
      setLoadFailures([]);
      setPendingUpdates([]);
    }
  }, [selectedProfileId]);

  useEffect(() => {
    void load();
  }, [load]);

  // Live pack-download process (ProcessRegistry mirror) — drives the progress bar + auto-refresh.
  const packDownload = useProcessStore((s) =>
    s.processes.find((p) => p.titleKey === "process.pluginDownload" && p.status === "running")
  );
  const completedDownloads = useProcessStore((s) =>
    s.processes.filter((p) => p.type === "download" && p.status === "completed").length
  );
  useEffect(() => {
    void load();
  }, [completedDownloads, load]);

  // Check for pack updates once the list is known — a background, non-fatal network call (GitHub
  // release manifest) so the plugin list never waits on it; re-checked after a download completes.
  useEffect(() => {
    if (!selectedProfileId || !plugins || plugins.length === 0) return;
    let cancelled = false;
    void (async () => {
      try {
        const list = await pluginService.checkUpdates(selectedProfileId);
        if (!cancelled) setUpdates(Object.fromEntries(list.map((u) => [u.pluginId, u])));
      } catch {
        /* offline / no release — no update badges */
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [selectedProfileId, plugins, completedDownloads]);

  const handleToggle = async (plugin: PluginInfo, enabled: boolean) => {
    if (!selectedProfileId) return;
    setBusyId(plugin.id);
    try {
      await pluginService.setEnabled(selectedProfileId, plugin.id, enabled);
      await load();
    } catch (error) {
      handleError(error);
    } finally {
      setBusyId(undefined);
    }
  };

  const handleDownloadPack = async (packId: string) => {
    if (!selectedProfileId) return;
    try {
      await pluginService.downloadPack(selectedProfileId, packId);
      notification.info(t("settings.plugins.pack.started"));
    } catch (error) {
      handleError(error);
    }
  };

  // Update re-downloads the latest pack. The loaded dll is locked, so it stages and applies on restart.
  const handleUpdatePack = async (packId: string) => {
    if (!selectedProfileId) return;
    try {
      await pluginService.downloadPack(selectedProfileId, packId);
      notification.info(t("settings.plugins.update.staged"));
    } catch (error) {
      handleError(error);
    }
  };

  // Restart the app to apply staged plugin updates — reuses the app-update relaunch (the launcher
  // applies any staged app update too, else just relaunches; the next load swaps in .pending packs).
  const handleRestart = async () => {
    try {
      await systemService.restartForUpdate();
    } catch (error) {
      handleError(error);
    }
  };

  const handleOpenFolder = async () => {
    if (!directory) return;
    try {
      // The plugins path is a DIRECTORY — open it directly. openFileInExplorer validates File.Exists,
      // which is false for a directory, so it threw "File not found" on the (existing) plugins folder.
      await systemService.openDirectory(directory);
    } catch (error) {
      handleError(error);
    }
  };

  // The backend flags `installed` per pack (from the registry) — show only what isn't installed yet.
  // A load-FAILED pack is installed-on-disk but unregistered (installed=false), so it would otherwise
  // double-show here AND in the "failed to load" section above — exclude it (its update is offered there).
  const uninstalledPacks = availablePacks.filter(
    (pack) => !pack.installed && !loadFailures.some((f) => f.packId === pack.id),
  );

  return (
    <div className="settings-view-profile">
      <CompactCard
        title={<><ApiOutlined /> {t("settings.plugins.title")}</>}
        extra={
          <div className="plugin-tab__header-actions">
            <CompactButton icon={<FolderOpenOutlined />} onClick={() => void handleOpenFolder()}>
              {t("settings.plugins.folder.open")}
            </CompactButton>
            <CompactButton icon={<ReloadOutlined />} onClick={() => void load()}>
              {t("common.refresh")}
            </CompactButton>
          </div>
        }
      >
        {/* Staged updates awaiting restart (mirrors the app-update "ready — restart" pill). */}
        {pendingUpdates.length > 0 && (
          <div className="plugin-pending-banner">
            <ExclamationCircleOutlined className="plugin-pending-banner__icon" />
            <span className="plugin-pending-banner__text">
              {t("settings.plugins.pending.banner", { count: pendingUpdates.length })}
            </span>
            <CompactButton type="primary" icon={<ReloadOutlined />} onClick={() => void handleRestart()}>
              {t("update.ready.restartNow")}
            </CompactButton>
          </div>
        )}

        {/* Failed-to-load packs — installed but incompatible (usually after an app update). */}
        {loadFailures.length > 0 && (
          <>
            <div className="plugin-section-label">{t("settings.plugins.failedSection")}</div>
            <div className="plugin-list">
              {loadFailures.map((failure) => {
                const downloading = packDownload?.titleArg === failure.packId;
                return (
                  <div key={failure.packId} className="plugin-card plugin-card--failed">
                    <div className="plugin-card__icon"><ExclamationCircleOutlined /></div>
                    <div className="plugin-card__body">
                      <div className="plugin-card__title-row">
                        <span className="plugin-card__name">{failure.name ?? failure.packId}</span>
                        <StatusTag tone="warning" icon={null} label={t("settings.plugins.loadFailed.requiresUpdate")} />
                      </div>
                      <div className="plugin-card__desc">
                        {failure.updateAvailable
                          ? t("settings.plugins.loadFailed.hint")
                          : t("settings.plugins.loadFailed.noUpdate")}
                      </div>
                      <div className="plugin-card__reason">{failure.reason}</div>
                    </div>
                    <div className="plugin-card__action">
                      {failure.updateAvailable && (
                        <CompactButton
                          type="primary"
                          icon={<CloudDownloadOutlined />}
                          loading={downloading}
                          onClick={() => void handleUpdatePack(failure.packId)}
                        >
                          {t("settings.plugins.loadFailed.download")}
                        </CompactButton>
                      )}
                    </div>
                  </div>
                );
              })}
            </div>
          </>
        )}

        {/* Installed plugins */}
        <div className="plugin-section-label">{t("settings.plugins.loadedSection")}</div>
        {plugins && plugins.length === 0 ? (
          <div className="plugin-empty">
            <ApiOutlined className="plugin-empty__icon" />
            <div className="plugin-empty__title">{t("settings.plugins.empty")}</div>
            <div className="plugin-empty__hint">{t("settings.plugins.emptyHint")}</div>
          </div>
        ) : (
          <div className="plugin-list">
            {(plugins ?? []).map((plugin) => (
              <div key={plugin.id} className={`plugin-card${plugin.isEnabled ? "" : " plugin-card--disabled"}`}>
                <div className="plugin-card__icon">
                  {plugin.capabilities.map((c) => CAPABILITY_ICON[c]).find(Boolean) ?? <ApiOutlined />}
                </div>
                <div className="plugin-card__body">
                  <div className="plugin-card__title-row">
                    <span className="plugin-card__name">{plugin.name}</span>
                    <span className="plugin-card__version">v{plugin.version}</span>
                    {updates[plugin.id]?.updateAvailable && (
                      <StatusTag
                        tone="warning"
                        icon={null}
                        label={t("settings.plugins.update.available", {
                          version: updates[plugin.id].availableVersion,
                        })}
                      />
                    )}
                    {plugin.capabilities.map((capability) => (
                      <StatusTag
                        key={capability}
                        tone="info"
                        icon={null}
                        label={t(`settings.plugins.capability.${capability}`, capability)}
                      />
                    ))}
                  </div>
                  <div className="plugin-card__desc">{plugin.description}</div>
                  <div className="plugin-card__meta">{t("settings.plugins.by", { author: plugin.author })}</div>
                </div>
                <div className="plugin-card__action">
                  {updates[plugin.id]?.updateAvailable && (
                    <CompactButton
                      icon={<CloudDownloadOutlined />}
                      loading={packDownload?.titleArg === updates[plugin.id].packId}
                      onClick={() => void handleUpdatePack(updates[plugin.id].packId)}
                    >
                      {t("settings.plugins.pack.update")}
                    </CompactButton>
                  )}
                  <CompactSwitch
                    checked={plugin.isEnabled}
                    loading={busyId === plugin.id}
                    onChange={(checked: boolean) => void handleToggle(plugin, checked)}
                    checkedChildren={t("common.enable")}
                    unCheckedChildren={t("common.disable")}
                  />
                </div>
              </div>
            ))}
          </div>
        )}

        {/* Available packs (official downloads) */}
        {uninstalledPacks.length > 0 && (
          <>
            <div className="plugin-section-label">{t("settings.plugins.availableSection")}</div>
            <div className="plugin-list">
              {uninstalledPacks.map((pack) => {
                const downloading = packDownload?.titleArg === pack.id;
                return (
                  <div key={pack.id} className="plugin-card plugin-card--available">
                    <div className="plugin-card__icon"><CloudDownloadOutlined /></div>
                    <div className="plugin-card__body">
                      <div className="plugin-card__title-row">
                        <span className="plugin-card__name">{pack.name}</span>
                        <span className="plugin-card__version">v{pack.version}</span>
                        {!pack.compatible && (
                          <StatusTag tone="warning" icon={null} label={t("settings.plugins.pack.incompatible")} />
                        )}
                      </div>
                      <div className="plugin-card__desc">{pack.description}</div>
                      {downloading && (
                        <Progress
                          percent={packDownload?.progress ?? 0}
                          size="small"
                          status="active"
                          className="plugin-card__progress"
                        />
                      )}
                    </div>
                    <div className="plugin-card__action">
                      <CompactButton
                        type="primary"
                        icon={<CloudDownloadOutlined />}
                        loading={downloading}
                        disabled={!pack.compatible}
                        onClick={() => void handleDownloadPack(pack.id)}
                      >
                        {t("settings.plugins.pack.download")}
                      </CompactButton>
                    </div>
                  </div>
                );
              })}
            </div>
          </>
        )}

        <div className="plugin-tab__footer-hint">
          {t("settings.plugins.folder.hint", { path: directory ?? "…" })}
        </div>
      </CompactCard>
    </div>
  );
};

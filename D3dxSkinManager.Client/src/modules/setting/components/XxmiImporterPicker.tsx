import React, { useState, useEffect, useCallback } from 'react';
import { Space } from 'antd';
import { FolderOpenOutlined, LoadingOutlined, CheckCircleOutlined, DownloadOutlined, GlobalOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { CompactButton, CompactAlert, CompactSelect } from '../../../shared/components/compact';
import { StatusTag } from '../../../shared/components/common/StatusTag';
import { ConfirmDialog } from '../../../shared/components/dialogs/ConfirmDialog';
import { KeyValueRows } from '../../../shared/components/common/KeyValueRows';
import { systemService, launchService } from '../../../shared/services/ipc';
import type { XxmiDetectResult, XxmiImporter, XxmiInstallerInfo } from '../../../shared/services/ipc/launchService';
import { logger } from '../../../shared/utils/logger';
import { handleError } from '../../../shared/utils/errorHandler';
import { notification } from '../../../shared/utils/notification';
import { formatBytes } from '../../../shared/utils/formatBytes';
import './XxmiImporterPicker.css';

/** The launcher's public release page — the browser fallback for the download assist. */
const XXMI_RELEASES_URL = 'https://github.com/SpectrumQT/XXMI-Launcher/releases/latest';

interface XxmiImporterPickerProps {
  profileId?: string;
  /** Current external work directory — used to auto-detect + preselect the bound importer. */
  currentDirectory?: string;
  /**
   * The importer dir the profile is actually SAVED to (baseline, not the live field). When given, the
   * picker shows a Bound / Not-applied tag so a selection that was never confirmed is visibly distinct
   * from the persisted binding.
   */
  boundDirectory?: string;
  /**
   * Called when the user picks an importer; hands back the importer folder (= work dir), its Mods
   * path, the XXMI Launcher exe (= launch target), and the importer NAME (e.g. "ZZMI") so the launch
   * command can be auto-derived (`--nogui --xxmi <NAME>`). One pick sets work dir + launch.
   */
  onSelect: (importerDir: string, modsDir: string, launcherExe?: string, importerName?: string) => void;
  /** Fires after every successful detect — lets the parent enrich its display (game folder, config path). */
  onDetect?: (result: XxmiDetectResult) => void;
}

/**
 * L3 connected helper for the Mod Working Directory setting: point at an XXMI Launcher folder, pick the
 * game importer, and we hand its folder back to the parent as the external work directory (so loaded
 * mods deploy into `<importer>\Mods`). Selecting only updates the parent's work-dir field — the normal
 * Save persists it. See .claude/rules/xxmi-integration.md.
 */
export const XxmiImporterPicker: React.FC<XxmiImporterPickerProps> = ({
  profileId,
  currentDirectory,
  boundDirectory,
  onSelect,
  onDetect,
}) => {
  const { t } = useTranslation();
  const [detect, setDetect] = useState<XxmiDetectResult | undefined>(undefined);
  const [importerName, setImporterName] = useState<string | undefined>(undefined);
  const [detecting, setDetecting] = useState(false);
  const [detectFailed, setDetectFailed] = useState(false);
  // "Get XXMI" assist (users with no install yet): look up the latest installer, confirm, download.
  const [installerLookup, setInstallerLookup] = useState(false);
  const [installerInfo, setInstallerInfo] = useState<XxmiInstallerInfo | undefined>(undefined);

  const askInstall = useCallback(async () => {
    if (!profileId) return;
    try {
      setInstallerLookup(true);
      setInstallerInfo(await launchService.getXxmiInstaller(profileId));
    } catch (error) {
      handleError(error);
    } finally {
      setInstallerLookup(false);
    }
  }, [profileId]);

  const confirmInstall = useCallback(async () => {
    if (!profileId || !installerInfo) return;
    try {
      await launchService.downloadXxmiInstaller(profileId, installerInfo);
      notification.info(t('launch.xxmi.get.started'));
    } catch (error) {
      handleError(error);
    } finally {
      setInstallerInfo(undefined);
    }
  }, [profileId, installerInfo, t]);

  const runDetect = useCallback(async (folder: string, preselectDir?: string, silent = false) => {
    if (!profileId) return;
    setDetecting(true);
    setDetectFailed(false);
    try {
      const res = await launchService.detectXxmi(profileId, folder);
      setDetect(res);
      onDetect?.(res);
      const match = preselectDir ? res.importers.find((i) => i.importerDir === preselectDir) : undefined;
      setImporterName(match?.name ?? res.importers.find((i) => i.isActive)?.name ?? res.importers[0]?.name);
    } catch (error) {
      // The startup auto-detect must not toast (the user didn't act) — surface inline instead.
      setDetect(undefined);
      setDetectFailed(true);
      if (silent) {
        logger.warn('[XxmiImporterPicker] auto-detect failed:', error);
      } else {
        handleError(error);
      }
    } finally {
      setDetecting(false);
    }
  }, [profileId, onDetect]);

  // If the work dir already points inside an XXMI install, auto-populate the list + preselect.
  useEffect(() => {
    if (currentDirectory && !detect && !detectFailed) {
      void runDetect(currentDirectory, currentDirectory, true);
    }
  }, [currentDirectory, detect, detectFailed, runDetect]);

  const pickFolder = useCallback(async () => {
    try {
      const res = await systemService.openFolderDialog({
        title: t('launch.xxmi.folderLabel'),
        rememberPathKey: 'xxmiFolder',
      });
      if (res.success && res.filePath) await runDetect(res.filePath);
    } catch (error) {
      handleError(error);
    }
  }, [t, runDetect]);

  const onChange = useCallback((name: string) => {
    setImporterName(name);
    const imp = detect?.importers.find((i) => i.name === name);
    if (imp) onSelect(imp.importerDir, imp.modsDir, detect?.launcherExe, imp.name);
  }, [detect, onSelect]);

  const selected = detect?.importers.find((i) => i.name === importerName);
  const options = (detect?.importers ?? []).map((i) => ({ value: i.name, label: i.name, importer: i }));
  const isBound = !!boundDirectory && selected?.importerDir === boundDirectory;

  return (
    <div className="xxmi-picker">
      <Space.Compact style={{ width: '100%' }}>
        <CompactSelect
          style={{ flex: 1 }}
          placeholder={t('launch.xxmi.importerPlaceholderSettings')}
          value={importerName}
          onChange={onChange}
          loading={detecting}
          notFoundContent={detect ? t('launch.xxmi.noImporters') : null}
          options={options}
          optionRender={(opt) => {
            const imp = (opt.data as { importer: XxmiImporter }).importer;
            return (
              <Space>
                <span>{imp.name}</span>
                {boundDirectory && imp.importerDir === boundDirectory && (
                  <StatusTag tone="processing" icon={null} label={t('launch.xxmi.boundTag')} />
                )}
                {imp.isActive && <StatusTag tone="success" icon={null} label={t('launch.xxmi.active')} />}
                {!imp.isInstalled && <StatusTag tone="neutral" icon={null} label={t('launch.xxmi.notInstalled')} />}
              </Space>
            );
          }}
        />
        <CompactButton icon={<FolderOpenOutlined />} loading={detecting} onClick={pickFolder}>
          {t('launch.xxmi.pickFolder')}
        </CompactButton>
      </Space.Compact>

      {detecting && (
        <div className="xxmi-picker__status">
          <LoadingOutlined spin /> {t('launch.xxmi.detecting')}
        </div>
      )}
      {!detecting && detect?.found && (
        <div className="xxmi-picker__status xxmi-picker__status--ok">
          <CheckCircleOutlined /> {t('launch.xxmi.detected', { count: detect.importers.length })}
        </div>
      )}
      {!detecting && detectFailed && (
        <CompactAlert
          type="warning"
          showIcon
          extraCompact
          message={t('launch.xxmi.detectFailed')}
        />
      )}

      {selected && (
        <div className="xxmi-picker__target">
          <span>
            {t('launch.xxmi.modsTarget')}: <code>{selected.modsDir}</code>
          </span>
          {boundDirectory !== undefined && (
            isBound
              ? <StatusTag tone="success" label={t('launch.xxmi.boundTag')} />
              : <StatusTag tone="warning" label={t('launch.xxmi.notApplied')} />
          )}
        </div>
      )}

      {/* "Get XXMI" assist — only while nothing is detected (fresh users). Downloads the official
          installer in the background and opens it; installing/launching stays XXMI's job. */}
      {!detecting && !detect?.found && (
        <div className="xxmi-picker__get">
          <span className="xxmi-picker__get-hint">{t('launch.xxmi.get.hint')}</span>
          <CompactButton
            size="small"
            icon={<DownloadOutlined />}
            loading={installerLookup}
            onClick={() => void askInstall()}
          >
            {t('launch.xxmi.get.download')}
          </CompactButton>
          <CompactButton
            size="small"
            type="text"
            icon={<GlobalOutlined />}
            onClick={() => void systemService.openUrl(XXMI_RELEASES_URL)}
          >
            {t('launch.xxmi.get.openPage')}
          </CompactButton>
        </div>
      )}

      <ConfirmDialog
        visible={!!installerInfo}
        title={t('launch.xxmi.get.confirmTitle')}
        onOk={confirmInstall}
        onCancel={() => setInstallerInfo(undefined)}
        content={
          installerInfo && (
            <KeyValueRows
              boxed
              rows={[
                { label: t('launch.xxmi.get.confirmVersion'), value: installerInfo.version },
                { label: t('launch.xxmi.get.confirmFile'), value: installerInfo.fileName },
                { label: t('launch.xxmi.get.confirmSize'), value: formatBytes(installerInfo.sizeBytes) },
              ]}
              hint={t('launch.xxmi.get.confirmHint')}
            />
          )
        }
      />
    </div>
  );
};

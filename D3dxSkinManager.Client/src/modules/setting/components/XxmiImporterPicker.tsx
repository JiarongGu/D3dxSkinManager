import React, { useState, useEffect, useCallback } from 'react';
import { Select, Space } from 'antd';
import { FolderOpenOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { CompactButton } from '../../../shared/components/compact';
import { StatusTag } from '../../../shared/components/common/StatusTag';
import { systemService, launchService } from '../../../shared/services/ipc';
import type { XxmiDetectResult, XxmiImporter } from '../../../shared/services/ipc/launchService';
import { handleError } from '../../../shared/utils/errorHandler';
import './XxmiImporterPicker.css';

interface XxmiImporterPickerProps {
  profileId?: string;
  /** Current external work directory — used to auto-detect + preselect the bound importer. */
  currentDirectory?: string;
  /**
   * Called when the user picks an importer; hands back the importer folder (= work dir), its Mods
   * path, the XXMI Launcher exe (= launch target), and the importer NAME (e.g. "ZZMI") so the launch
   * command can be auto-derived (`--nogui --xxmi <NAME>`). One pick sets work dir + launch.
   */
  onSelect: (importerDir: string, modsDir: string, launcherExe?: string, importerName?: string) => void;
}

/**
 * L3 connected helper for the Mod Working Directory setting: point at an XXMI Launcher folder, pick the
 * game importer, and we hand its folder back to the parent as the external work directory (so loaded
 * mods deploy into `<importer>\Mods`). Selecting only updates the parent's work-dir field — the normal
 * Save persists it. See .claude/rules/xxmi-integration.md.
 */
export const XxmiImporterPicker: React.FC<XxmiImporterPickerProps> = ({ profileId, currentDirectory, onSelect }) => {
  const { t } = useTranslation();
  const [detect, setDetect] = useState<XxmiDetectResult | undefined>(undefined);
  const [importerName, setImporterName] = useState<string | undefined>(undefined);
  const [detecting, setDetecting] = useState(false);

  const runDetect = useCallback(async (folder: string, preselectDir?: string) => {
    if (!profileId) return;
    setDetecting(true);
    try {
      const res = await launchService.detectXxmi(profileId, folder);
      setDetect(res);
      const match = preselectDir ? res.importers.find((i) => i.importerDir === preselectDir) : undefined;
      setImporterName(match?.name ?? res.importers.find((i) => i.isActive)?.name ?? res.importers[0]?.name);
    } catch (error) {
      handleError(error);
    } finally {
      setDetecting(false);
    }
  }, [profileId]);

  // If the work dir already points inside an XXMI install, auto-populate the list + preselect.
  useEffect(() => {
    if (currentDirectory && !detect) {
      void runDetect(currentDirectory, currentDirectory);
    }
  }, [currentDirectory, detect, runDetect]);

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

  return (
    <div className="xxmi-picker">
      <Space.Compact style={{ width: '100%' }}>
        <Select
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
      {selected && (
        <div className="xxmi-picker__target">
          {t('launch.xxmi.modsTarget')}: <code>{selected.modsDir}</code>
        </div>
      )}
    </div>
  );
};

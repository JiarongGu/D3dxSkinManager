import React, { useEffect, useRef, useState } from 'react';
import { Segmented, Spin } from 'antd';
import { useTranslation } from 'react-i18next';
import { FormDialog } from '../../../shared/components/dialogs';
import { KeyValueRows } from '../../../shared/components/common/KeyValueRows';
import {
  CompactAlert,
  CompactButton,
  CompactCheckbox,
  CompactField,
  CompactInput,
} from '../../../shared/components/compact';
import { profileService, systemService } from '../../../shared/services/ipc';
import { handleError } from '../../../shared/utils/errorHandler';
import { notification } from '../../../shared/utils/notification';
import { Module, ProfileEventType } from '../../../shared/services/eventBus';
import { useEventSubscription } from '../../../shared/hooks/useEventSubscription';
import type { ProfileBundleAnalysis, ProfileBundleImportResult } from '../../../shared/types/profileBundle.types';
import './ProfileImportDialog.css';

type WorkMode = 'internal' | 'external' | 'xxmi';

interface ProfileImportDialogProps {
  /** The selected bundle (folder or .zip). Setting it opens the dialog + triggers analysis. */
  bundlePath?: string;
  onClose: () => void;
  onImported: () => void;
}

/**
 * Import-a-profile-settings-bundle dialog (L3). Analyzes the chosen bundle for a preview, collects a
 * name + which parts to import + the new profile's mod-work mode, then fires the import
 * (fire-and-forget) and waits for PROFILE/IMPORT_SETTINGS_COMPLETE before closing. Import always creates
 * a NEW profile; the backend resets machine-specific config, so the work-mode choice here is applied
 * after import (external = point at a folder now; XXMI = finish in Settings → Mod Work).
 */
export const ProfileImportDialog: React.FC<ProfileImportDialogProps> = ({ bundlePath, onClose, onImported }) => {
  const { t } = useTranslation();
  const [analyzing, setAnalyzing] = useState(false);
  const [analysis, setAnalysis] = useState<ProfileBundleAnalysis>();
  const [newName, setNewName] = useState('');
  const [importCategories, setImportCategories] = useState(true);
  const [importRemote, setImportRemote] = useState(true);
  const [workMode, setWorkMode] = useState<WorkMode>('internal');
  const [workDir, setWorkDir] = useState<string>();
  const completeRef = useRef<((r: ProfileBundleImportResult) => void) | null>(null);

  // Analyze whenever a new bundle is chosen.
  useEffect(() => {
    if (!bundlePath) return;
    let cancelled = false;
    setAnalysis(undefined);
    setWorkMode('internal');
    setWorkDir(undefined);
    void (async () => {
      try {
        setAnalyzing(true);
        const result = await profileService.analyzeBundle(bundlePath);
        if (cancelled) return;
        setAnalysis(result);
        setNewName(result.profileName || '');
        setImportCategories(result.categoryCount > 0);
        setImportRemote(result.libraryCount > 0);
      } catch (error: unknown) {
        if (!cancelled) handleError(error);
      } finally {
        if (!cancelled) setAnalyzing(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [bundlePath]);

  // Import runs fire-and-forget; the result arrives here (see profileService.importSettings).
  useEventSubscription(Module.PROFILE, ProfileEventType.IMPORT_SETTINGS_COMPLETE, (payload) => {
    if (!payload) return;
    completeRef.current?.(payload);
    completeRef.current = null;
  });

  const chooseWorkDir = async () => {
    try {
      const r = await systemService.openFolderDialog({
        title: t('profiles.bundle.workMode.chooseFolder'),
        rememberPathKey: 'profile-workdir',
      });
      if (r.success && r.filePath) setWorkDir(r.filePath);
    } catch (error: unknown) {
      handleError(error);
    }
  };

  const handleImport = async () => {
    if (!bundlePath || !analysis?.isValid) return;
    if (workMode === 'external' && !workDir) {
      await chooseWorkDir();
      return; // let the user confirm again once a folder is chosen
    }

    notification.info(t('profiles.notifications.importStarted'));
    const resultPromise = new Promise<ProfileBundleImportResult>((resolve) => {
      completeRef.current = resolve;
    });
    try {
      await profileService.importSettings({
        bundlePath,
        newProfileName: newName.trim() || undefined,
        importCategories,
        importRemote,
      });
    } catch (error: unknown) {
      completeRef.current = null;
      handleError(error);
      return;
    }

    const result = await resultPromise;
    if (!result.success) {
      notification.error(t('profiles.notifications.importFailed'));
      return; // keep the dialog open
    }

    try {
      if (workMode === 'external' && workDir) {
        await profileService.updateProfileConfig({
          profileId: result.newProfileId,
          workMode: 'external',
          workDirectory: workDir,
        });
      } else if (workMode === 'xxmi') {
        notification.info(t('profiles.bundle.workMode.hint.xxmi'));
      }
    } catch (error: unknown) {
      handleError(error);
    }

    notification.success(t('profiles.notifications.importSuccess', { name: result.profileName }));
    onImported();
    onClose();
  };

  const summaryRows = analysis?.isValid
    ? [
        ...(analysis.gameName ? [{ label: t('profiles.bundle.summary.game'), value: analysis.gameName }] : []),
        { label: t('profiles.bundle.summary.categories'), value: String(analysis.categoryCount) },
        { label: t('profiles.bundle.summary.libraries'), value: String(analysis.libraryCount) },
        ...(analysis.hasThumbnail
          ? [{ label: t('profiles.bundle.summary.thumbnail'), value: t('profiles.bundle.summary.included') }]
          : []),
        { label: t('profiles.bundle.summary.version'), value: analysis.version },
      ]
    : [];

  return (
    <FormDialog
      visible={bundlePath !== undefined}
      title={t('profiles.bundle.importTitle')}
      okText={t('profiles.bundle.import')}
      onOk={handleImport}
      onCancel={onClose}
      width={520}
    >
      <Spin spinning={analyzing} tip={t('profiles.bundle.analyzing')}>
        <div className="profile-import-dialog">
          {analysis && !analysis.isValid && (
            <CompactAlert type="error" message={analysis.errorMessage || t('profiles.bundle.invalid')} />
          )}

          {analysis?.isValid && (
            <>
              <KeyValueRows boxed rows={summaryRows} />

              <CompactField label={t('profiles.bundle.newName')}>
                <CompactInput value={newName} onChange={(e) => setNewName(e.target.value)} />
              </CompactField>

              <div className="profile-import-dialog__options">
                <CompactCheckbox
                  checked={importCategories}
                  disabled={analysis.categoryCount === 0}
                  onChange={(e) => setImportCategories(e.target.checked)}
                >
                  {`${t('profiles.bundle.importCategories')} (${analysis.categoryCount})`}
                </CompactCheckbox>
                <CompactCheckbox
                  checked={importRemote}
                  disabled={analysis.libraryCount === 0}
                  onChange={(e) => setImportRemote(e.target.checked)}
                >
                  {`${t('profiles.bundle.importRemote')} (${analysis.libraryCount})`}
                </CompactCheckbox>
              </div>

              <CompactField label={t('profiles.bundle.workMode.label')} hint={t(`profiles.bundle.workMode.hint.${workMode}`)}>
                <Segmented
                  block
                  value={workMode}
                  onChange={(v) => setWorkMode(v as WorkMode)}
                  options={[
                    { label: t('profiles.bundle.workMode.internal'), value: 'internal' },
                    { label: t('profiles.bundle.workMode.external'), value: 'external' },
                    { label: t('profiles.bundle.workMode.xxmi'), value: 'xxmi' },
                  ]}
                />
              </CompactField>

              {workMode === 'external' && (
                <div className="profile-import-dialog__folder">
                  <CompactButton onClick={chooseWorkDir}>{t('profiles.bundle.workMode.chooseFolder')}</CompactButton>
                  {workDir && <span className="profile-import-dialog__folder-path">{workDir}</span>}
                </div>
              )}

              <CompactAlert type="info" message={t('profiles.bundle.excludeNote')} />
            </>
          )}
        </div>
      </Spin>
    </FormDialog>
  );
};

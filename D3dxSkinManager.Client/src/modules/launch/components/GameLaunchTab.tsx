import React, { useState, useEffect, useCallback } from 'react';
import { Button, Input, Space } from 'antd';
import { PlayCircleOutlined, FolderOpenOutlined, CheckCircleFilled, RocketOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { useProfile } from '../../../shared/context/ProfileContext';
import { profileService, systemService } from '../../../shared/services/ipc';
import { notification } from '../../../shared/utils/notification';
import { handleError } from '../../../shared/utils/errorHandler';
import './GameLaunchTab.css';

/**
 * Game launch config + one-click launch for the active profile. Point it at the game exe, an XXMI
 * launcher, or a 3DMigoto loader; the app runs it via SYSTEM/LAUNCH_PROCESS. Config persists in the
 * profile's config (ProfileConfiguration.Launch).
 */
export const GameLaunchTab: React.FC = () => {
  const { t } = useTranslation();
  const { selectedProfileId, selectedProfile } = useProfile();
  const [path, setPath] = useState('');
  const [args, setArgs] = useState('');
  const [savedPath, setSavedPath] = useState('');
  const [savedArgs, setSavedArgs] = useState('');
  const [saving, setSaving] = useState(false);
  const [justSaved, setJustSaved] = useState(false);

  const load = useCallback(async () => {
    if (!selectedProfileId) return;
    try {
      const cfg = await profileService.getProfileConfig(selectedProfileId);
      const p = cfg?.launch?.path ?? '';
      const a = cfg?.launch?.args ?? '';
      setPath(p); setArgs(a); setSavedPath(p); setSavedArgs(a);
    } catch (error) {
      handleError(error);
    }
  }, [selectedProfileId]);
  useEffect(() => { void load(); }, [load]);

  const dirty = path.trim() !== savedPath || args.trim() !== savedArgs;

  const browse = useCallback(async () => {
    try {
      const res = await systemService.openFileDialog({
        title: t('launch.pickExe'),
        filters: [{ name: t('launch.exeFilter'), extensions: ['exe', 'bat', 'cmd', 'lnk'] }],
        rememberPathKey: 'launchExe',
      });
      if (res.success && res.filePath) setPath(res.filePath);
    } catch (error) {
      handleError(error);
    }
  }, [t]);

  const save = useCallback(async () => {
    if (!selectedProfileId) return;
    setSaving(true);
    try {
      await profileService.updateProfileConfig({ profileId: selectedProfileId, launchPath: path.trim(), launchArgs: args.trim() });
      setSavedPath(path.trim()); setSavedArgs(args.trim());
      setJustSaved(true);
      window.setTimeout(() => setJustSaved(false), 2000);
    } catch (error) {
      handleError(error);
    } finally {
      setSaving(false);
    }
  }, [selectedProfileId, path, args]);

  const launch = useCallback(async () => {
    if (!savedPath) return;
    try {
      await systemService.launchProcess(savedPath, savedArgs || undefined);
      notification.info(t('launch.launching'));
    } catch {
      notification.error(t('launch.failed'));
    }
  }, [savedPath, savedArgs, t]);

  return (
    <div className="launch-game">
      {/* Hero: identity + the primary launch action */}
      <div className="launch-game__hero">
        <div className="launch-game__hero-icon"><RocketOutlined /></div>
        <div className="launch-game__hero-text">
          <div className="launch-game__hero-title">{t('launch.view.title')}</div>
          <div className="launch-game__hero-sub">
            {selectedProfile?.gameName || selectedProfile?.name || t('launch.view.noProfile')}
          </div>
        </div>
        <Button
          type="primary"
          size="large"
          icon={<PlayCircleOutlined />}
          disabled={!savedPath}
          onClick={launch}
          className="launch-game__launch-btn"
        >
          {t('launch.launch')}
        </Button>
      </div>
      {!savedPath && <div className="launch-game__hint">{t('launch.view.notConfiguredHint')}</div>}

      {/* Launch target config */}
      <div className="launch-game__card">
        <div className="launch-game__card-title">{t('launch.view.targetTitle')}</div>
        <div className="launch-game__card-desc">{t('launch.view.targetDesc')}</div>

        <label className="launch-game__label">{t('launch.view.pathLabel')}</label>
        <Space.Compact style={{ width: '100%' }}>
          <Input value={path} placeholder={t('launch.pathPlaceholder')} onChange={(e) => setPath(e.target.value)} />
          <Button icon={<FolderOpenOutlined />} onClick={browse}>{t('launch.browse')}</Button>
        </Space.Compact>

        <label className="launch-game__label">{t('launch.view.argsLabel')}</label>
        <Input value={args} placeholder={t('launch.argsPlaceholder')} onChange={(e) => setArgs(e.target.value)} />

        <div className="launch-game__actions">
          <Button type="primary" onClick={save} loading={saving} disabled={!dirty || !selectedProfileId}>
            {t('common.save')}
          </Button>
          {justSaved && (
            <span className="launch-game__saved"><CheckCircleFilled /> {t('launch.view.saved')}</span>
          )}
        </div>
      </div>
    </div>
  );
};

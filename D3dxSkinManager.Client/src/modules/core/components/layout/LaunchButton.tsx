import React, { useState, useEffect, useCallback } from 'react';
import { Button } from 'antd';
import { PlayCircleOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { useProfile } from '../../../../shared/context/ProfileContext';
import { profileService, systemService } from '../../../../shared/services/ipc';
import { eventBus, Module, ProfileEventType } from '../../../../shared/services/eventBus';
import { notification } from '../../../../shared/utils/notification';
import { navigateToTab } from '../../../../shared/hooks/useAppNavigation';

/**
 * Status-bar quick-launch. When a launch target is configured (Settings → Mod Work / XXMI picker),
 * one click runs it via SYSTEM/LAUNCH_PROCESS. When NOT configured (e.g. a freshly-upgraded library),
 * it still shows as a "Set up launch" call-to-action that routes to Settings — so the button never
 * silently disappears just because no path is set yet.
 */
export const LaunchButton: React.FC = () => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();
  const [path, setPath] = useState('');
  const [args, setArgs] = useState('');

  const loadConfig = useCallback(async () => {
    if (!selectedProfileId) { setPath(''); setArgs(''); return; }
    try {
      const cfg = await profileService.getProfileConfig(selectedProfileId);
      setPath(cfg?.launch?.path ?? '');
      setArgs(cfg?.launch?.args ?? '');
    } catch {
      setPath(''); setArgs('');
    }
  }, [selectedProfileId]);
  useEffect(() => { void loadConfig(); }, [loadConfig]);

  // The launch command is editable in Settings → Mod Work (and set by the XXMI bind) — refresh on save.
  useEffect(() => {
    const unsubscribe = eventBus.subscribe(Module.PROFILE, ProfileEventType.CONFIG_UPDATED, () => {
      void loadConfig();
    });
    return unsubscribe;
  }, [loadConfig]);

  const onClick = useCallback(async () => {
    // Not configured yet → take the user to Settings (Mod Work / XXMI picker) to set a launch target.
    if (!path) { navigateToTab('settings'); return; }
    try {
      await systemService.launchProcess(path, args || undefined);
      notification.info(t('launch.launching'));
    } catch {
      notification.error(t('launch.failed'));
    }
  }, [path, args, t]);

  if (!selectedProfileId) return null;

  // Configured → primary launch button. Not configured → a subtle "set up launch" prompt (so an
  // upgraded library without a launch path still surfaces the action instead of hiding it).
  if (!path) {
    return (
      <Button size="small" icon={<PlayCircleOutlined />} onClick={onClick} title={t('launch.configureHint')}>
        {t('launch.setup')}
      </Button>
    );
  }

  return (
    <Button type="primary" size="small" icon={<PlayCircleOutlined />} onClick={onClick} title={path}>
      {t('launch.launch')}
    </Button>
  );
};

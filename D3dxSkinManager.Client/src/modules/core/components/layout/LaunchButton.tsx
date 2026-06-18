import React, { useState, useEffect, useCallback } from 'react';
import { Button } from 'antd';
import { PlayCircleOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { useProfile } from '../../../../shared/context/ProfileContext';
import { profileService, systemService } from '../../../../shared/services/ipc';
import { navigateToTab } from '../../../../shared/hooks/useAppNavigation';
import { notification } from '../../../../shared/utils/notification';

/**
 * Status-bar quick-launch. When a launch target is configured (Launch tab), one click runs it via
 * SYSTEM/LAUNCH_PROCESS; otherwise it routes to the Launch tab to configure it. The config itself
 * lives in the Launch tab — this is just the shortcut.
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

  const onClick = useCallback(async () => {
    if (!path) { navigateToTab('launch'); return; }
    try {
      await systemService.launchProcess(path, args || undefined);
      notification.info(t('launch.launching'));
    } catch {
      notification.error(t('launch.failed'));
    }
  }, [path, args, t]);

  if (!selectedProfileId) return null;

  return (
    <Button
      type={path ? 'primary' : 'default'}
      size="small"
      icon={<PlayCircleOutlined />}
      onClick={onClick}
      title={path ? path : t('launch.setup')}
    >
      {path ? t('launch.launch') : t('launch.setup')}
    </Button>
  );
};

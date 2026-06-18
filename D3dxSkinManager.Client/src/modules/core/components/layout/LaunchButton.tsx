import React, { useState, useEffect, useCallback } from 'react';
import { Button, Dropdown, Modal, Input, Space } from 'antd';
import { PlayCircleOutlined, SettingOutlined, FolderOpenOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { useProfile } from '../../../../shared/context/ProfileContext';
import { profileService, systemService } from '../../../../shared/services/ipc';
import { notification } from '../../../../shared/utils/notification';
import { handleError } from '../../../../shared/utils/errorHandler';

/**
 * Per-profile game launch control in the status bar. Reads the profile's launch config
 * (path + args, e.g. an XXMI launcher / 3DMigoto loader / game exe); the button runs it, the
 * dropdown reconfigures it. When unconfigured, the button opens the config modal.
 */
export const LaunchButton: React.FC = () => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();
  const [path, setPath] = useState('');
  const [args, setArgs] = useState('');
  const [configOpen, setConfigOpen] = useState(false);
  const [draftPath, setDraftPath] = useState('');
  const [draftArgs, setDraftArgs] = useState('');

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

  const openConfig = useCallback(() => {
    setDraftPath(path);
    setDraftArgs(args);
    setConfigOpen(true);
  }, [path, args]);

  const launch = useCallback(async () => {
    if (!path) { openConfig(); return; }
    try {
      await systemService.launchProcess(path, args || undefined);
      notification.info(t('launch.launching'));
    } catch {
      notification.error(t('launch.failed'));
    }
  }, [path, args, t, openConfig]);

  const browse = useCallback(async () => {
    try {
      const res = await systemService.openFileDialog({
        title: t('launch.pickExe'),
        filters: [{ name: t('launch.exeFilter'), extensions: ['exe', 'bat', 'cmd', 'lnk'] }],
        rememberPathKey: 'launchExe',
      });
      if (res.success && res.filePath) setDraftPath(res.filePath);
    } catch (error) {
      handleError(error);
    }
  }, [t]);

  const save = useCallback(async () => {
    if (!selectedProfileId) return;
    try {
      await profileService.updateProfileConfig({ profileId: selectedProfileId, launchPath: draftPath.trim(), launchArgs: draftArgs.trim() });
      setConfigOpen(false);
      await loadConfig();
    } catch (error) {
      handleError(error);
    }
  }, [selectedProfileId, draftPath, draftArgs, loadConfig]);

  if (!selectedProfileId) return null;

  return (
    <>
      {path ? (
        <Dropdown.Button
          type="primary"
          size="small"
          icon={<SettingOutlined />}
          onClick={launch}
          menu={{ items: [{ key: 'config', label: t('launch.configure'), onClick: openConfig }] }}
        >
          <PlayCircleOutlined /> {t('launch.launch')}
        </Dropdown.Button>
      ) : (
        <Button size="small" icon={<PlayCircleOutlined />} onClick={openConfig}>
          {t('launch.setup')}
        </Button>
      )}

      <Modal
        title={t('launch.configTitle')}
        open={configOpen}
        onOk={save}
        onCancel={() => setConfigOpen(false)}
        okText={t('common.save')}
        cancelText={t('common.cancel')}
        okButtonProps={{ disabled: !draftPath.trim() }}
      >
        <Space direction="vertical" style={{ width: '100%' }} size="small">
          <Space.Compact style={{ width: '100%' }}>
            <Input value={draftPath} placeholder={t('launch.pathPlaceholder')} onChange={(e) => setDraftPath(e.target.value)} />
            <Button icon={<FolderOpenOutlined />} onClick={browse}>{t('launch.browse')}</Button>
          </Space.Compact>
          <Input value={draftArgs} placeholder={t('launch.argsPlaceholder')} onChange={(e) => setDraftArgs(e.target.value)} />
        </Space>
      </Modal>
    </>
  );
};

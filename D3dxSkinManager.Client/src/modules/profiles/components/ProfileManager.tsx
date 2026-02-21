import React, { useState, useEffect } from 'react';
import {
  Modal,
  Flex,
  Button,
  Space,
  Tag,
  Tooltip,
  Popconfirm,
  Form,
  Input,
  ColorPicker,
  Spin,
  message
} from 'antd';
import {
  PlusOutlined,
  EditOutlined,
  DeleteOutlined,
  CopyOutlined,
  FolderOpenOutlined,
  CheckCircleOutlined,
  DownloadOutlined
} from '@ant-design/icons';
import classNames from 'classnames';
import { useTranslation } from 'react-i18next';
import { profileService } from '../../profiles/services/profileService';
import { Profile, CreateProfileRequest } from '../../../shared/types/profile.types';
import { fileDialogService } from '../../../shared/services/systemService';
import { useProfile } from '../../../shared/context/ProfileContext';
import styles from './ProfileManager.module.css';

interface ProfileManagerProps {
  onProfileChanged?: () => void;
}

export const ProfileManager: React.FC<ProfileManagerProps> = ({
  onProfileChanged
}) => {
  const { t } = useTranslation();
  const { state, actions } = useProfile();
  const [profiles, setProfiles] = useState<Profile[]>([]);
  const [activeProfileId, setActiveProfileId] = useState<string>('');
  const [loading, setLoading] = useState(false);
  const [editingProfile, setEditingProfile] = useState<Profile | null>(null);
  const [showCreateDialog, setShowCreateDialog] = useState(false);
  const [createForm] = Form.useForm();
  const [editForm] = Form.useForm();

  useEffect(() => {
    loadProfiles();
  }, []);

  const loadProfiles = async () => {
    try {
      setLoading(true);
      const result = await profileService.getAllProfiles();
      setProfiles(result.profiles);
      setActiveProfileId(result.activeProfileId);
    } catch (error) {
      console.error('Failed to load profiles:', error);
      message.error(t('profiles.notifications.loadFailed'));
    } finally {
      setLoading(false);
    }
  };

  const handleCreate = async () => {
    try {
      const values = await createForm.validateFields();

      const request: CreateProfileRequest = {
        name: values.name,
        description: values.description,
        workDirectory: values.workDirectory,
        colorTag: values.colorTag?.toHexString?.() || values.colorTag,
        gameName: values.gameName,
        copyFromCurrent: values.copyFromCurrent || false
      };

      await profileService.createProfile(request);
      message.success(t('profiles.notifications.createSuccess'));

      setShowCreateDialog(false);
      createForm.resetFields();
      await loadProfiles();

      if (onProfileChanged) {
        onProfileChanged();
      }
    } catch (error) {
      console.error('Failed to create profile:', error);
      message.error(t('profiles.notifications.createFailed'));
    }
  };

  const handleEdit = async () => {
    if (!editingProfile) return;

    try {
      const values = await editForm.validateFields();

      await profileService.updateProfile({
        profileId: editingProfile.id,
        name: values.name,
        description: values.description,
        workDirectory: values.workDirectory,
        colorTag: values.colorTag?.toHexString?.() || values.colorTag,
        gameName: values.gameName
      });

      message.success(t('profiles.notifications.updateSuccess'));
      setEditingProfile(null);
      await loadProfiles();

      // Reload profiles in the context to update ProfileSwitcher
      await actions.loadProfiles();

      // If the edited profile is the currently selected one, update it in context
      if (editingProfile.id === state.selectedProfile?.id) {
        const updatedProfile = await profileService.getProfileById(editingProfile.id);
        if (updatedProfile) {
          actions.setSelectedProfile(updatedProfile);
        }
      }

      if (onProfileChanged) {
        onProfileChanged();
      }
    } catch (error) {
      console.error('Failed to update profile:', error);
      message.error(t('profiles.notifications.updateFailed'));
    }
  };

  const handleDelete = async (profileId: string) => {
    try {
      await profileService.deleteProfile(profileId);
      message.success(t('profiles.notifications.deleteSuccess'));
      await loadProfiles();

      if (onProfileChanged) {
        onProfileChanged();
      }
    } catch (error: unknown) {
      console.error('Failed to delete profile:', error);
      const errorMessage = error instanceof Error ? error.message : t('profiles.notifications.deleteFailed');
      message.error(errorMessage);
    }
  };

  const handleDuplicate = async (profile: Profile) => {
    try {
      const newName = `${profile.name}${t('profiles.duplicate.suffix')}`;
      await profileService.duplicateProfile(profile.id, newName);
      message.success(t('profiles.notifications.duplicateSuccess'));
      await loadProfiles();

      if (onProfileChanged) {
        onProfileChanged();
      }
    } catch (error) {
      console.error('Failed to duplicate profile:', error);
      message.error(t('profiles.notifications.duplicateFailed'));
    }
  };

  const handleBrowseWorkDirectory = async (formInstance: any) => {
    try {
      const result = await fileDialogService.openFolderDialog({
        title: t('profiles.dialog.selectWorkDirectory')
      });

      if (result.success && result.filePath) {
        formInstance.setFieldsValue({ workDirectory: result.filePath });
      }
    } catch (error) {
      message.error(t('profiles.notifications.folderDialogFailed'));
    }
  };

  const handleExport = async (profileId: string) => {
    try {
      const configJson = await profileService.exportProfileConfig(profileId);
      const blob = new Blob([configJson], { type: 'application/json' });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `profile-${profileId}.json`;
      a.click();
      URL.revokeObjectURL(url);
      message.success(t('profiles.notifications.exportSuccess'));
    } catch (error) {
      console.error('Failed to export profile:', error);
      message.error(t('profiles.notifications.exportFailed'));
    }
  };

  return (
    <>
      <div className={styles.container}>
        <Space orientation="vertical" className={styles.verticalSpace} size="large">
          <Button
            type="primary"
            icon={<PlusOutlined />}
            onClick={() => setShowCreateDialog(true)}
          >
            {t('profiles.button.createNew')}
          </Button>

          <Spin spinning={loading}>
            <Flex vertical gap="middle">
              {profiles.map((profile) => (
                <Flex
                  key={profile.id}
                  justify="space-between"
                  align="center"
                  className={classNames(styles.profileItem, {
                    [styles.profileItemActive]: profile.id === activeProfileId,
                    [styles.profileItemInactive]: profile.id !== activeProfileId,
                  })}
                >
                  <Flex align="flex-start" gap="middle" className={styles.profileContent}>
                    <div
                      className={styles.profileAvatar}
                      style={{ backgroundColor: profile.colorTag || '#1890ff' }}
                    >
                      {profile.name.charAt(0).toUpperCase()}
                    </div>
                    <Flex vertical gap="small" className={styles.profileContent}>
                      <Space>
                        <span className={styles.profileName}>{profile.name}</span>
                        {profile.id === activeProfileId && (
                          <Tag color="success" icon={<CheckCircleOutlined />}>
                            {t('profiles.badge.active')}
                          </Tag>
                        )}
                        {profile.gameName && (
                          <Tag color="blue">{profile.gameName}</Tag>
                        )}
                      </Space>
                      {profile.description && (
                        <span className={styles.profileDescription}>{profile.description}</span>
                      )}
                      <Space size="large">
                        <span className={styles.profileStats}>
                          {t('profiles.label.mods')} {profile.modCount}
                        </span>
                        <span className={styles.profileStats}>
                          {t('profiles.label.size')} {profileService.formatBytes(profile.totalSize || 0)}
                        </span>
                        <span className={styles.profileStats}>
                          {t('profiles.label.created')} {new Date(profile.createdAt).toLocaleDateString()}
                        </span>
                      </Space>
                    </Flex>
                  </Flex>
                  <Space>
                    <Tooltip title={t('profiles.tooltip.edit')}>
                      <Button
                        icon={<EditOutlined />}
                        size="small"
                        onClick={() => {
                          setEditingProfile(profile);
                          editForm.setFieldsValue({
                            name: profile.name,
                            description: profile.description,
                            workDirectory: profile.workDirectory,
                            colorTag: profile.colorTag,
                            gameName: profile.gameName
                          });
                        }}
                      />
                    </Tooltip>
                    <Tooltip title={t('profiles.tooltip.duplicate')}>
                      <Button
                        icon={<CopyOutlined />}
                        size="small"
                        onClick={() => handleDuplicate(profile)}
                      />
                    </Tooltip>
                    <Tooltip title={t('profiles.tooltip.exportConfig')}>
                      <Button
                        icon={<DownloadOutlined />}
                        size="small"
                        onClick={() => handleExport(profile.id)}
                      />
                    </Tooltip>
                    {profile.id !== activeProfileId && (
                      <Popconfirm
                        title={t('profiles.delete.title')}
                        description={t('profiles.delete.description')}
                        onConfirm={() => handleDelete(profile.id)}
                        okText={t('profiles.delete.confirm')}
                        cancelText={t('common.cancel')}
                        okButtonProps={{ danger: true }}
                      >
                        <Tooltip title={t('profiles.tooltip.delete')}>
                          <Button
                            icon={<DeleteOutlined />}
                            size="small"
                            danger
                          />
                        </Tooltip>
                      </Popconfirm>
                    )}
                  </Space>
                </Flex>
              ))}
            </Flex>
          </Spin>
        </Space>
      </div>

      {/* Create Profile Dialog */}
      <Modal
        title={t('profiles.dialog.createTitle')}
        open={showCreateDialog}
        onCancel={() => {
          setShowCreateDialog(false);
          createForm.resetFields();
        }}
        onOk={handleCreate}
        width={600}
      >
        <Form form={createForm} layout="vertical">
          <Form.Item
            label={t('profiles.form.name.label')}
            name="name"
            rules={[{ required: true, message: t('profiles.form.name.required') }]}
          >
            <Input placeholder={t('profiles.form.name.placeholder')} />
          </Form.Item>

          <Form.Item
            label={t('profiles.form.description.label')}
            name="description"
          >
            <Input.TextArea
              rows={2}
              placeholder={t('profiles.form.description.placeholder')}
            />
          </Form.Item>

          <Form.Item
            label={t('profiles.form.workDirectory.label')}
            name="workDirectory"
            rules={[{ required: true, message: t('profiles.form.workDirectory.required') }]}
          >
            <Space.Compact className={styles.fullWidthInput}>
              <Input
                placeholder={t('profiles.form.workDirectory.placeholder')}
                className={styles.fullWidthInput}
              />
              <Button
                icon={<FolderOpenOutlined />}
                onClick={() => handleBrowseWorkDirectory(createForm)}
              />
            </Space.Compact>
          </Form.Item>

          <Form.Item
            label={t('profiles.form.gameName.label')}
            name="gameName"
          >
            <Input placeholder={t('profiles.form.gameName.placeholder')} />
          </Form.Item>

          <Form.Item
            label={t('profiles.form.colorTag.label')}
            name="colorTag"
          >
            <ColorPicker showText />
          </Form.Item>
        </Form>
      </Modal>

      {/* Edit Profile Dialog */}
      <Modal
        title={t('profiles.dialog.editTitle')}
        open={editingProfile !== null}
        onCancel={() => setEditingProfile(null)}
        onOk={handleEdit}
        width={600}
      >
        <Form form={editForm} layout="vertical">
          <Form.Item
            label={t('profiles.form.name.label')}
            name="name"
            rules={[{ required: true, message: t('profiles.form.name.required') }]}
          >
            <Input />
          </Form.Item>

          <Form.Item
            label={t('profiles.form.description.label')}
            name="description"
          >
            <Input.TextArea rows={2} />
          </Form.Item>

          <Form.Item
            label={t('profiles.form.workDirectory.label')}
            name="workDirectory"
            rules={[{ required: true, message: t('profiles.form.workDirectory.required') }]}
          >
            <Space.Compact className={styles.fullWidthInput}>
              <Input className={styles.fullWidthInput} />
              <Button
                icon={<FolderOpenOutlined />}
                onClick={() => handleBrowseWorkDirectory(editForm)}
              />
            </Space.Compact>
          </Form.Item>

          <Form.Item
            label={t('profiles.form.gameName.label')}
            name="gameName"
          >
            <Input />
          </Form.Item>

          <Form.Item
            label={t('profiles.form.colorTag.label')}
            name="colorTag"
          >
            <ColorPicker showText />
          </Form.Item>
        </Form>
      </Modal>
    </>
  );
};

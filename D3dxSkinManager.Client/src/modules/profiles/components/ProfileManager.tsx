import React, { useState, useEffect } from 'react';
import {
  Modal,
  List,
  Button,
  Space,
  Tag,
  Tooltip,
  Popconfirm,
  Form,
  Input,
  ColorPicker,
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
import { profileService } from '../../profiles/services/profileService';
import { Profile, CreateProfileRequest } from '../../../shared/types/profile.types';
import { fileDialogService } from '../../../shared/services/fileDialogService';

interface ProfileManagerProps {
  visible: boolean;
  onClose: () => void;
  onProfileChanged?: () => void;
}

export const ProfileManager: React.FC<ProfileManagerProps> = ({
  visible,
  onClose,
  onProfileChanged
}) => {
  const [profiles, setProfiles] = useState<Profile[]>([]);
  const [activeProfileId, setActiveProfileId] = useState<string>('');
  const [loading, setLoading] = useState(false);
  const [editingProfile, setEditingProfile] = useState<Profile | null>(null);
  const [showCreateDialog, setShowCreateDialog] = useState(false);
  const [createForm] = Form.useForm();
  const [editForm] = Form.useForm();

  useEffect(() => {
    if (visible) {
      loadProfiles();
    }
  }, [visible]);

  const loadProfiles = async () => {
    try {
      setLoading(true);
      const result = await profileService.getAllProfiles();
      setProfiles(result.profiles);
      setActiveProfileId(result.activeProfileId);
    } catch (error) {
      console.error('Failed to load profiles:', error);
      message.error('Failed to load profiles');
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
      message.success('Profile created successfully');

      setShowCreateDialog(false);
      createForm.resetFields();
      await loadProfiles();

      if (onProfileChanged) {
        onProfileChanged();
      }
    } catch (error) {
      console.error('Failed to create profile:', error);
      message.error('Failed to create profile');
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

      message.success('Profile updated successfully');
      setEditingProfile(null);
      await loadProfiles();

      if (onProfileChanged) {
        onProfileChanged();
      }
    } catch (error) {
      console.error('Failed to update profile:', error);
      message.error('Failed to update profile');
    }
  };

  const handleDelete = async (profileId: string) => {
    try {
      await profileService.deleteProfile(profileId);
      message.success('Profile deleted successfully');
      await loadProfiles();

      if (onProfileChanged) {
        onProfileChanged();
      }
    } catch (error: unknown) {
      console.error('Failed to delete profile:', error);
      const errorMessage = error instanceof Error ? error.message : 'Failed to delete profile';
      message.error(errorMessage);
    }
  };

  const handleDuplicate = async (profile: Profile) => {
    try {
      const newName = `${profile.name} (Copy)`;
      await profileService.duplicateProfile(profile.id, newName);
      message.success('Profile duplicated successfully');
      await loadProfiles();

      if (onProfileChanged) {
        onProfileChanged();
      }
    } catch (error) {
      console.error('Failed to duplicate profile:', error);
      message.error('Failed to duplicate profile');
    }
  };

  const handleBrowseWorkDirectory = async (formInstance: any) => {
    try {
      const result = await fileDialogService.openFolderDialog({
        title: 'Select Work Directory (3DMigoto location)'
      });

      if (result.success && result.filePath) {
        formInstance.setFieldsValue({ workDirectory: result.filePath });
      }
    } catch (error) {
      message.error('Failed to open folder dialog');
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
      message.success('Profile configuration exported');
    } catch (error) {
      console.error('Failed to export profile:', error);
      message.error('Failed to export profile');
    }
  };

  return (
    <>
      <Modal
        title="Profile Manager"
        open={visible}
        onCancel={onClose}
        width={900}
        footer={[
          <Button key="close" onClick={onClose}>
            Close
          </Button>
        ]}
      >
        <Space orientation="vertical" style={{ width: '100%' }} size="large">
          <Button
            type="primary"
            icon={<PlusOutlined />}
            onClick={() => setShowCreateDialog(true)}
          >
            Create New Profile
          </Button>

          <List
            loading={loading}
            dataSource={profiles}
            renderItem={(profile) => (
              <List.Item
                key={profile.id}
                style={{
                  borderLeft: profile.id === activeProfileId ? '4px solid #52c41a' : '4px solid #d9d9d9',
                  paddingLeft: '16px'
                }}
              >
                <List.Item.Meta
                  avatar={
                    <div
                      style={{
                        width: '40px',
                        height: '40px',
                        borderRadius: '8px',
                        backgroundColor: profile.colorTag || '#1890ff',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        color: 'white',
                        fontWeight: 'bold',
                        fontSize: '18px'
                      }}
                    >
                      {profile.name.charAt(0).toUpperCase()}
                    </div>
                  }
                  title={
                    <Space>
                      <span style={{ fontWeight: 500, fontSize: '16px' }}>{profile.name}</span>
                      {profile.id === activeProfileId && (
                        <Tag color="success" icon={<CheckCircleOutlined />}>
                          Active
                        </Tag>
                      )}
                      {profile.gameName && (
                        <Tag color="blue">{profile.gameName}</Tag>
                      )}
                    </Space>
                  }
                  description={
                    <Space orientation="vertical" size={4} style={{ width: '100%' }}>
                      {profile.description && (
                        <span style={{ color: '#595959' }}>{profile.description}</span>
                      )}
                      <Space size="large">
                        <span style={{ fontSize: '12px', color: '#8c8c8c' }}>
                          Mods: {profile.modCount}
                        </span>
                        <span style={{ fontSize: '12px', color: '#8c8c8c' }}>
                          Size: {profileService.formatBytes(profile.totalSize || 0)}
                        </span>
                        <span style={{ fontSize: '12px', color: '#8c8c8c' }}>
                          Created: {new Date(profile.createdAt).toLocaleDateString()}
                        </span>
                      </Space>
                    </Space>
                  }
                />
                <Space>
                  <Tooltip title="Edit">
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
                  <Tooltip title="Duplicate">
                    <Button
                      icon={<CopyOutlined />}
                      size="small"
                      onClick={() => handleDuplicate(profile)}
                    />
                  </Tooltip>
                  <Tooltip title="Export Config">
                    <Button
                      icon={<DownloadOutlined />}
                      size="small"
                      onClick={() => handleExport(profile.id)}
                    />
                  </Tooltip>
                  {profile.id !== activeProfileId && (
                    <Popconfirm
                      title="Delete Profile?"
                      description="This will permanently delete all profile data including mods. Are you sure?"
                      onConfirm={() => handleDelete(profile.id)}
                      okText="Delete"
                      cancelText="Cancel"
                      okButtonProps={{ danger: true }}
                    >
                      <Tooltip title="Delete">
                        <Button
                          icon={<DeleteOutlined />}
                          size="small"
                          danger
                        />
                      </Tooltip>
                    </Popconfirm>
                  )}
                </Space>
              </List.Item>
            )}
          />
        </Space>
      </Modal>

      {/* Create Profile Dialog */}
      <Modal
        title="Create New Profile"
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
            label="Profile Name"
            name="name"
            rules={[{ required: true, message: 'Please enter profile name' }]}
          >
            <Input placeholder="e.g., Genshin Impact, Endfield, etc." />
          </Form.Item>

          <Form.Item
            label="Description"
            name="description"
          >
            <Input.TextArea
              rows={2}
              placeholder="Optional description for this profile"
            />
          </Form.Item>

          <Form.Item
            label="Work Directory"
            name="workDirectory"
            rules={[{ required: true, message: 'Please select work directory' }]}
          >
            <Input
              placeholder="Directory where 3DMigoto d3dx.dll is loaded"
              addonAfter={
                <Button
                  type="link"
                  icon={<FolderOpenOutlined />}
                  onClick={() => handleBrowseWorkDirectory(createForm)}
                  style={{ padding: 0 }}
                />
              }
            />
          </Form.Item>

          <Form.Item
            label="Game Name"
            name="gameName"
          >
            <Input placeholder="e.g., Genshin Impact" />
          </Form.Item>

          <Form.Item
            label="Color Tag"
            name="colorTag"
          >
            <ColorPicker showText />
          </Form.Item>
        </Form>
      </Modal>

      {/* Edit Profile Dialog */}
      <Modal
        title="Edit Profile"
        open={editingProfile !== null}
        onCancel={() => setEditingProfile(null)}
        onOk={handleEdit}
        width={600}
      >
        <Form form={editForm} layout="vertical">
          <Form.Item
            label="Profile Name"
            name="name"
            rules={[{ required: true, message: 'Please enter profile name' }]}
          >
            <Input />
          </Form.Item>

          <Form.Item
            label="Description"
            name="description"
          >
            <Input.TextArea rows={2} />
          </Form.Item>

          <Form.Item
            label="Work Directory"
            name="workDirectory"
            rules={[{ required: true, message: 'Please select work directory' }]}
          >
            <Input
              addonAfter={
                <Button
                  type="link"
                  icon={<FolderOpenOutlined />}
                  onClick={() => handleBrowseWorkDirectory(editForm)}
                  style={{ padding: 0 }}
                />
              }
            />
          </Form.Item>

          <Form.Item
            label="Game Name"
            name="gameName"
          >
            <Input />
          </Form.Item>

          <Form.Item
            label="Color Tag"
            name="colorTag"
          >
            <ColorPicker showText />
          </Form.Item>
        </Form>
      </Modal>
    </>
  );
};

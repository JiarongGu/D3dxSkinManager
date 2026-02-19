import React from 'react';
import { Select, Button, Space, Spin } from 'antd';
import { UserOutlined, SettingOutlined } from '@ant-design/icons';
import { useProfile } from '../../../shared/context/ProfileContext';

interface ProfileSelectorProps {
  onManageProfiles?: () => void;
}

/**
 * Profile selector component for the header
 * Shows current profile and allows switching
 */
export const ProfileSelector: React.FC<ProfileSelectorProps> = ({ onManageProfiles }) => {
  const { state, actions } = useProfile();

  const handleProfileChange = async (profileId: string) => {
    try {
      await actions.selectProfile(profileId);
    } catch (error) {
      console.error('Failed to switch profile:', error);
    }
  };

  if (state.loading && !state.selectedProfile) {
    return <Spin size="small" />;
  }

  return (
    <Space>
      <UserOutlined style={{ color: '#1890ff' }} />
      <Select
        value={state.selectedProfile?.id}
        onChange={handleProfileChange}
        style={{ width: 150 }}
        size="small"
        loading={state.loading}
        options={state.profiles.map(profile => ({
          label: profile.name,
          value: profile.id
        }))}
      />
      {onManageProfiles && (
        <Button
          type="text"
          size="small"
          icon={<SettingOutlined />}
          onClick={onManageProfiles}
        />
      )}
    </Space>
  );
};

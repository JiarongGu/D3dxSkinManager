import React from 'react';
import { Select, Button, Space, Spin } from 'antd';
import { UserOutlined, SettingOutlined } from '@ant-design/icons';
import { useProfile } from '../../../shared/context/ProfileContext';
import './ProfileSelector.css';

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
      // Error already handled by ProfileContext
    }
  };

  if (state.loading && !state.selectedProfile) {
    return <Spin size="small" />;
  }

  return (
    <Space>
      <UserOutlined className="profile-selector-icon" />
      <Select
        value={state.selectedProfile?.id}
        onChange={handleProfileChange}
        className="profile-selector-dropdown"
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

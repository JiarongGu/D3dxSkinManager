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
  const { selectedProfile, selectedProfileId, profiles, loading, actions } = useProfile();

  const handleProfileChange = async (profileId: string) => {
    try {
      await actions.selectProfile(profileId);
    } catch (error: unknown) {
      // Error already handled by ProfileContext
    }
  };

  if (loading && !selectedProfile) {
    return <Spin size="small" />;
  }

  return (
    <Space>
      <UserOutlined className="profile-selector-icon" />
      <Select
        value={selectedProfileId}
        onChange={handleProfileChange}
        className="profile-selector-dropdown"
        size="small"
        loading={loading}
        options={profiles.map(profile => ({
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

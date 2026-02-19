import React from 'react';
import { Dropdown, Button, Space, message, Badge } from 'antd';
import type { MenuProps } from 'antd';
import {
  FolderOpenOutlined,
  CheckCircleFilled,
  SettingOutlined,
  ThunderboltOutlined
} from '@ant-design/icons';
import { useProfile } from '../../../shared/context/ProfileContext';
import { Profile } from '../../../shared/types/profile.types';

interface ProfileSwitcherProps {
  onManageClick?: () => void;
  onProfileSwitch?: (profile: Profile) => void;
}

export const ProfileSwitcher: React.FC<ProfileSwitcherProps> = ({
  onManageClick,
  onProfileSwitch
}) => {
  const { state, actions } = useProfile();

  const handleProfileSwitch = async (profileId: string) => {
    if (profileId === state.selectedProfile?.id) {
      return; // Already selected
    }

    try {
      await actions.selectProfile(profileId);
      message.success(`Switched to profile`);

      // Notify parent component
      if (onProfileSwitch && state.selectedProfile) {
        onProfileSwitch(state.selectedProfile);
      }

      // Reload the page to refresh mod data
      window.location.reload();
    } catch (error) {
      console.error('Failed to switch profile:', error);
      message.error('Failed to switch profile');
    }
  };

  const activeProfile = state.selectedProfile;

  // Ensure profiles is an array
  const profiles = Array.isArray(state.profiles) ? state.profiles : [];

  const menuItems: MenuProps['items'] = [
    {
      key: 'profiles-header',
      label: (
        <Space style={{ width: '100%', justifyContent: 'space-between', padding: '4px 0' }}>
          <span style={{ fontWeight: 'bold', fontSize: '11px', color: '#8c8c8c', letterSpacing: '0.5px' }}>
            MOD PROFILES
          </span>
          <Badge count={profiles.length} style={{ backgroundColor: '#1890ff', fontSize: '10px' }} />
        </Space>
      ),
      disabled: true
    },
    { type: 'divider' },
    ...profiles.map(profile => ({
      key: profile.id,
      label: (
        <Space style={{ width: '100%', justifyContent: 'space-between', padding: '2px 0' }}>
          <Space>
            <FolderOpenOutlined style={{ color: '#1890ff', fontSize: '14px' }} />
            <span style={{ fontWeight: profile.id === state.selectedProfile?.id ? 600 : 400 }}>
              {profile.name}
            </span>
            {profile.modCount !== undefined && (
              <span style={{ color: '#8c8c8c', fontSize: '12px' }}>
                ({profile.modCount} mods)
              </span>
            )}
          </Space>
          {profile.id === state.selectedProfile?.id && (
            <CheckCircleFilled style={{ color: '#52c41a', fontSize: '16px' }} />
          )}
        </Space>
      ),
      onClick: () => handleProfileSwitch(profile.id)
    })),
    { type: 'divider' },
    {
      key: 'manage',
      label: (
        <Space>
          <SettingOutlined />
          <span style={{ fontWeight: 500 }}>Manage Profiles</span>
        </Space>
      ),
      onClick: () => {
        if (onManageClick) {
          onManageClick();
        }
      }
    }
  ];

  return (
    <Dropdown
      menu={{ items: menuItems }}
      trigger={['click']}
      placement="bottomRight"
      disabled={state.loading}
    >
      <Button
        icon={<ThunderboltOutlined />}
        style={{
          background: 'rgba(24, 144, 255, 0.1)',
          border: '1px solid rgba(24, 144, 255, 0.3)',
          color: 'white',
          display: 'flex',
          alignItems: 'center',
          height: '100%',
          padding: '4px 12px',
          borderRadius: '4px',
          transition: 'all 0.3s'
        }}
        loading={state.loading}
        onMouseEnter={(e) => {
          e.currentTarget.style.background = 'rgba(24, 144, 255, 0.2)';
          e.currentTarget.style.borderColor = 'rgba(24, 144, 255, 0.5)';
        }}
        onMouseLeave={(e) => {
          e.currentTarget.style.background = 'rgba(24, 144, 255, 0.1)';
          e.currentTarget.style.borderColor = 'rgba(24, 144, 255, 0.3)';
        }}
      >
        <Space size={8}>
          {activeProfile && (
            <>
              <span style={{ fontWeight: 500, fontSize: '13px' }}>{activeProfile.name}</span>
              {activeProfile.modCount !== undefined && activeProfile.modCount > 0 && (
                <Badge
                  count={activeProfile.modCount}
                  style={{
                    backgroundColor: '#52c41a',
                    fontSize: '10px',
                    height: '18px',
                    lineHeight: '18px',
                    padding: '0 6px'
                  }}
                />
              )}
            </>
          )}
          {!activeProfile && <span style={{ fontWeight: 500 }}>Select Profile</span>}
        </Space>
      </Button>
    </Dropdown>
  );
};

import { notification } from '../../../shared/utils/notification';
import React, { useState } from 'react';
import { Button, Avatar } from 'antd';
import {
  UserOutlined,
  SettingOutlined
} from '@ant-design/icons';
import { useProfile } from '../../../shared/context/ProfileContext';
import { ContextMenu, ContextMenuItem } from '../../../shared/components/menu/ContextMenu';
import { Profile } from '../../../shared/types/profile.types';
import { useTranslation } from 'react-i18next';
import { toAppUrl } from '../../../shared/utils/imageUrlHelper';
import './ProfileSwitcher.css';

interface ProfileSwitcherProps {
  onManageClick?: () => void;
  onProfileSwitch?: (profile: Profile) => void;
}

export const ProfileSwitcher: React.FC<ProfileSwitcherProps> = ({
  onManageClick,
  onProfileSwitch
}) => {
  const { t } = useTranslation();
  const { state, actions } = useProfile();
  const [menuVisible, setMenuVisible] = useState(false);
  const [menuPosition, setMenuPosition] = useState({ x: 0, y: 0 });
  const buttonRef = React.useRef<HTMLButtonElement>(null);

  // Reload profiles when component mounts and when profiles list might have changed
  React.useEffect(() => {
    actions.loadProfiles();
  }, []);

  const handleProfileSwitch = async (profileId: string) => {
    if (profileId === state.selectedProfile?.id) {
      return; // Already selected
    }

    try {
      await actions.selectProfile(profileId);
      notification.success(t('profiles.notifications.switched'));

      // Notify parent component
      if (onProfileSwitch && state.selectedProfile) {
        onProfileSwitch(state.selectedProfile);
      }

      // NOTE: No manual refresh needed - ModsProvider reactively listens to profile changes
      // and will automatically refresh mods and Category tree
    } catch (error) {
      notification.error(t('profiles.notifications.switchFailed'));
    }
  };

  const activeProfile = state.selectedProfile;

  // Ensure profiles is an array
  const profiles = Array.isArray(state.profiles) ? state.profiles : [];

  const renderProfileAvatar = (profile: Profile) => {
    if (profile.thumbnail) {
      return (
        <Avatar
          size={24}
          src={toAppUrl(profile.thumbnail) || undefined}
          style={{ flexShrink: 0 }}
        />
      );
    }
    return (
      <Avatar
        size={24}
        style={{
          backgroundColor: profile.color || '#1890ff',
          flexShrink: 0
        }}
      >
        {profile.name.charAt(0).toUpperCase()}
      </Avatar>
    );
  };

  const renderProfileLabel = (profile: Profile) => {
    const isActive = profile.id === state.selectedProfile?.id;
    return (
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', width: '100%' }}>
        <span>{profile.name}</span>
        {isActive && <span style={{ marginLeft: '12px', color: 'var(--color-success)' }}>✓</span>}
      </div>
    );
  };

  const menuItems: ContextMenuItem[] = [
    {
      key: 'profiles-header',
      label: `${t('profiles.switcher.header')} (${profiles.length})`,
      disabled: true
    },
    { type: 'divider' },
    ...profiles.map(profile => ({
      key: profile.id,
      label: renderProfileLabel(profile),
      icon: renderProfileAvatar(profile),
      onClick: () => {
        setMenuVisible(false);
        handleProfileSwitch(profile.id);
      }
    })),
    { type: 'divider' },
    {
      key: 'manage',
      label: t('profiles.switcher.manage'),
      icon: <SettingOutlined />,
      onClick: () => {
        setMenuVisible(false);
        if (onManageClick) {
          onManageClick();
        }
      }
    }
  ];

  return (
    <>
      <Button
        ref={buttonRef}
        className="profile-switcher-button"
        loading={state.loading}
        disabled={state.loading}
        onClick={() => {
          if (buttonRef.current) {
            const rect = buttonRef.current.getBoundingClientRect();
            // Position menu below the button, aligned to the right edge of button
            // Using rect.right as x will make the menu position from its right edge
            setMenuPosition({
              x: rect.right,
              y: rect.bottom + 4
            });
          }
          setMenuVisible(true);
        }}
      >
        <div className="profile-switcher-content">
          <div className="profile-switcher-avatar">
            {activeProfile ? (
              renderProfileAvatar(activeProfile)
            ) : (
              <Avatar size={24} icon={<UserOutlined />} />
            )}
          </div>
          <div className="profile-switcher-text">
            {activeProfile && (
              <span className="profile-switcher-name">{activeProfile.name}</span>
            )}
            {!activeProfile && <span className="profile-switcher-placeholder">{t('profiles.switcher.selectProfile')}</span>}
          </div>
        </div>
      </Button>

      <ContextMenu
        items={menuItems}
        visible={menuVisible}
        position={menuPosition}
        onClose={() => setMenuVisible(false)}
      />
    </>
  );
};
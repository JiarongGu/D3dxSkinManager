import { notification } from '../../../shared/utils/notification';
import React, { useState } from 'react';
import { Button, Space, Badge } from 'antd';
import {
  FolderOpenOutlined,
  SettingOutlined,
  ThunderboltOutlined
} from '@ant-design/icons';
import { useProfile } from '../../../shared/context/ProfileContext';
import { ContextMenu, ContextMenuItem } from '../../../shared/components/menu/ContextMenu';
import { Profile } from '../../../shared/types/profile.types';
import { useTranslation } from 'react-i18next';
import { useModsContext } from '../../mods/context/ModsContext';
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
  const modsContext = useModsContext();
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

      // Refresh mods data for the new profile instead of reloading the page
      if (modsContext?.actions) {
        await modsContext.actions.refreshMods();
        await modsContext.actions.refreshClassificationTree();
      }
    } catch (error) {
      console.error('Failed to switch profile:', error);
      notification.error(t('profiles.notifications.switchFailed'));
    }
  };

  const activeProfile = state.selectedProfile;

  // Ensure profiles is an array
  const profiles = Array.isArray(state.profiles) ? state.profiles : [];

  const menuItems: ContextMenuItem[] = [
    {
      key: 'profiles-header',
      label: `${t('profiles.switcher.header')} (${profiles.length})`,
      disabled: true
    },
    { type: 'divider' },
    ...profiles.map(profile => ({
      key: profile.id,
      label: `${profile.name}${profile.modCount !== undefined ? ` (${t('profiles.switcher.modCount', { count: profile.modCount })})` : ''}${profile.id === state.selectedProfile?.id ? ' ✓' : ''}`,
      icon: <FolderOpenOutlined />,
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
        icon={<ThunderboltOutlined />}
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
        <Space size={8}>
          {activeProfile && (
            <>
              <span className="profile-switcher-name">{activeProfile.name}</span>
              {activeProfile.modCount !== undefined && activeProfile.modCount > 0 && (
                <Badge
                  count={activeProfile.modCount}
                  className="profile-switcher-badge"
                />
              )}
            </>
          )}
          {!activeProfile && <span className="profile-switcher-placeholder">{t('profiles.switcher.selectProfile')}</span>}
        </Space>
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
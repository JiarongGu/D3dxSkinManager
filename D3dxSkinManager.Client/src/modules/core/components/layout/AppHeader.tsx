import React, { useState } from 'react';
import { Layout } from 'antd';
import { ProfileSwitcher } from '../../../profiles/components/ProfileSwitcher';
import { ProfileManager } from '../../../profiles/components/ProfileManager';

const { Header } = Layout;

export const AppHeader: React.FC = () => {
  const [showProfileManager, setShowProfileManager] = useState(false);

  const handleProfileSwitch = () => {
    // Profile switched, page will reload automatically
  };

  return (
    <>
      <Header
        style={{
          background: 'var(--color-header-bg)',
          padding: '0 24px',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          borderBottom: '1px solid var(--color-border-secondary)'
        }}
      >
        <div style={{ color: 'var(--color-header-text)', fontSize: '20px', fontWeight: 'bold' }}>
          D3dxSkinManager
        </div>

        <ProfileSwitcher
          onManageClick={() => setShowProfileManager(true)}
          onProfileSwitch={handleProfileSwitch}
        />
      </Header>

      <ProfileManager
        visible={showProfileManager}
        onClose={() => setShowProfileManager(false)}
        onProfileChanged={() => {
          // Optionally reload profiles in switcher
        }}
      />
    </>
  );
};

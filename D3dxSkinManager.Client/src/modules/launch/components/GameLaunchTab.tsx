import React from 'react';
import { Alert, Typography } from 'antd';
import { useTranslation } from 'react-i18next';

const { Title } = Typography;

/**
 * Game Launch Tab - Placeholder
 *
 * NOTE: Game launch functionality has been temporarily disabled as part of the profile restructuring.
 * The game launch configuration (gamePath, gameLaunchArgs, customProgramPath, customProgramArgs)
 * has been removed from ProfileConfiguration.
 *
 * This feature needs to be reimplemented with a new architecture.
 */
export const GameLaunchTab: React.FC = () => {
  const { t } = useTranslation();

  return (
    <div style={{ padding: '24px' }}>
      <Title level={3}>{t('launch.game.title', 'Game Launch')}</Title>
      <Alert
        message={t('launch.game.underDevelopment')}
        description={t('launch.game.underDevelopmentDesc')}
        type="info"
        showIcon
        style={{ marginTop: '16px' }}
      />
    </div>
  );
};

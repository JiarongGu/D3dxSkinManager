import React, { useState, useEffect } from 'react';
import { Tabs, Alert, Space } from 'antd';
import { RocketOutlined, PlayCircleOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { D3DMigotoTab } from './D3DMigotoTab';
import { GameLaunchTab } from './GameLaunchTab';
import { MODULE_NAMES } from '../../../shared/constants/ui.constants';
import './LaunchView.css';

const { TabPane } = Tabs;

export const LaunchView: React.FC = () => {
  const { t } = useTranslation();

  return (
    <div className="launch-view">
      <div className="launch-view-content">
        {/* Game/XXMI launch is the primary, recommended path; raw 3DMigoto deploy is legacy/advanced. */}
        <Tabs defaultActiveKey="game" size="large">
          <TabPane
            tab={
              <span>
                <PlayCircleOutlined />
                {t('launch.tabs.game')}
              </span>
            }
            key="game"
          >
            <GameLaunchTab />
          </TabPane>

          <TabPane
            tab={
              <span>
                <RocketOutlined />
                {t('launch.tabs.migotoLegacy', { name: MODULE_NAMES.MIGOTO })}
              </span>
            }
            key="3dmigoto"
          >
            <D3DMigotoTab />
          </TabPane>
        </Tabs>
      </div>
    </div>
  );
};

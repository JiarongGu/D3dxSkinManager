import React, { useState, useEffect } from 'react';
import { Tabs, Alert, Space } from 'antd';
import { RocketOutlined, PlayCircleOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { D3DMigotoTab } from './D3DMigotoTab';
import { GameLaunchTab } from './GameLaunchTab';
import './LaunchView.css';

const { TabPane } = Tabs;

export const LaunchView: React.FC = () => {
  const { t } = useTranslation();

  return (
    <div className="launch-view">
      <div className="launch-view-content">
        <Tabs defaultActiveKey="3dmigoto" size="large">
          <TabPane
            tab={
              <span>
                <RocketOutlined />
                {t('launch.tabs.migoto')}
              </span>
            }
            key="3dmigoto"
          >
            <D3DMigotoTab />
          </TabPane>

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
        </Tabs>
      </div>
    </div>
  );
};

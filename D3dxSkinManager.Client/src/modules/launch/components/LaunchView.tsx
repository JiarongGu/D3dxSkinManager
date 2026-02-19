import React, { useState, useEffect } from 'react';
import { Tabs, Alert, Space } from 'antd';
import { RocketOutlined, PlayCircleOutlined } from '@ant-design/icons';
import { D3DMigotoTab } from './D3DMigotoTab';
import { GameLaunchTab } from './GameLaunchTab';

const { TabPane } = Tabs;

export const LaunchView: React.FC = () => {
  return (
    <div style={{ height: '100%', overflow: 'auto', padding: '24px' }}>
      <div style={{ maxWidth: '1200px', margin: '0 auto' }}>
        <Tabs defaultActiveKey="3dmigoto" size="large">
          <TabPane
            tab={
              <span>
                <RocketOutlined />
                3DMigoto
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
                Game Launch
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

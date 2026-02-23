import React from 'react';
import { Layout, Menu } from 'antd';
import {
  AppstoreOutlined,
  ToolOutlined,
  ApiOutlined,
  SettingOutlined,
  RocketOutlined,
  PlayCircleOutlined
} from '@ant-design/icons';
import './AppSider.css';

const { Sider } = Layout;

interface AppSiderProps {
  selectedTab: string;
  onTabChange: (tab: string) => void;
}

export const AppSider: React.FC<AppSiderProps> = ({ selectedTab, onTabChange }) => {
  return (
    <Sider width={160} className="app-sider">
      <Menu
        mode="inline"
        selectedKeys={[selectedTab]}
        className="app-sider-menu"
        items={[
          {
            key: 'mods',
            icon: <AppstoreOutlined />,
            label: 'Mods',
            onClick: () => onTabChange('mods')
          },
          {
            key: 'launch',
            icon: <RocketOutlined />,
            label: 'Launch',
            onClick: () => onTabChange('launch')
          },
          {
            key: 'tools',
            icon: <ToolOutlined />,
            label: 'Tools',
            onClick: () => onTabChange('tools')
          },
          {
            key: 'plugins',
            icon: <ApiOutlined />,
            label: 'Plugins',
            onClick: () => onTabChange('plugins')
          },
          {
            key: 'settings',
            icon: <SettingOutlined />,
            label: 'Settings',
            onClick: () => onTabChange('settings')
          }
        ]}
      />
    </Sider>
  );
};

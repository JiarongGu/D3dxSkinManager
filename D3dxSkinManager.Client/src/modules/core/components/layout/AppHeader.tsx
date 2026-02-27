import React from "react";
import classNames from 'classnames';
import { Layout, Button } from "antd";
import {
  AppstoreOutlined,
  ToolOutlined,
  ApiOutlined,
  SettingOutlined,
  RocketOutlined,
} from "@ant-design/icons";
import { ProfileSwitcher } from "../../../profile/components/ProfileSwitcher";
import { ProfileManager } from "../../../profile/components/ProfileManager";
import { useSlideInScreenContext } from "../../../../shared/context/SlideInScreenContext";
import { useTranslation } from "react-i18next";
import "./AppHeader.css";

const { Header } = Layout;

interface AppHeaderProps {
  selectedTab?: string;
  onTabChange?: (tab: string) => void;
}

interface TabItem {
  key: string;
  icon: React.ReactNode;
  label: string;
}

export const AppHeader: React.FC<AppHeaderProps> = ({
  selectedTab,
  onTabChange,
}) => {
  const { openScreen } = useSlideInScreenContext();
  const { t } = useTranslation();

  const tabs: TabItem[] = [
    { key: "mods", icon: <AppstoreOutlined />, label: t('header.tabs.mods') },
    { key: "launch", icon: <RocketOutlined />, label: t('header.tabs.launch') },
    { key: "tools", icon: <ToolOutlined />, label: t('header.tabs.tools') },
    { key: "plugins", icon: <ApiOutlined />, label: t('header.tabs.plugins') },
    { key: "settings", icon: <SettingOutlined />, label: t('header.tabs.settings') },
  ];

  const handleProfileSwitch = () => {
    // Profile switched, page will reload automatically
  };

  const handleManageProfiles = () => {
    openScreen({
      title: t('header.profile.manage'),
      content: <ProfileManager />,
      width: "900px",
    });
  };

  return (
    <Header className="app-header">
      {selectedTab && onTabChange && (
        <div className="app-header-tabs">
          {tabs.map((tab) => {
            const isSelected = selectedTab === tab.key;

            return (
              <Button
                key={tab.key}
                type="text"
                icon={tab.icon}
                onClick={() => onTabChange(tab.key)}
                className={classNames('app-header-tab', { 'app-header-tab-selected': isSelected })}
              >
                {tab.label}
              </Button>
            );
          })}
        </div>
      )}

      <ProfileSwitcher
        onManageClick={handleManageProfiles}
        onProfileSwitch={handleProfileSwitch}
      />
    </Header>
  );
};

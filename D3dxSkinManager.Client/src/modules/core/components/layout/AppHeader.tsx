import React from "react";
import { Layout, Button } from "antd";
import {
  AppstoreOutlined,
  ToolOutlined,
  ApiOutlined,
  SettingOutlined,
  RocketOutlined,
} from "@ant-design/icons";
import { ProfileSwitcher } from "../../../profiles/components/ProfileSwitcher";
import { ProfileManager } from "../../../profiles/components/ProfileManager";
import { useSlideInScreen } from "../../../../shared/context/SlideInScreenContext";
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

const tabs: TabItem[] = [
  { key: "mods", icon: <AppstoreOutlined />, label: "Mods" },
  { key: "launch", icon: <RocketOutlined />, label: "Launch" },
  { key: "tools", icon: <ToolOutlined />, label: "Tools" },
  { key: "plugins", icon: <ApiOutlined />, label: "Plugins" },
  { key: "settings", icon: <SettingOutlined />, label: "Settings" },
];

export const AppHeader: React.FC<AppHeaderProps> = ({
  selectedTab,
  onTabChange,
}) => {
  const { openScreen } = useSlideInScreen();

  const handleProfileSwitch = () => {
    // Profile switched, page will reload automatically
  };

  const handleManageProfiles = () => {
    openScreen({
      title: "Profile Manager",
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
                className={`app-header-tab ${isSelected ? "app-header-tab-selected" : ""}`}
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

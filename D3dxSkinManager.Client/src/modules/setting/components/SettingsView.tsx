import React from "react";
import { Tabs } from "antd";
import { UserOutlined, SettingOutlined } from "@ant-design/icons";
import { useTranslation } from "react-i18next";
import { ProfileSettingsTab } from "./ProfileSettingsTab";
import { GlobalSettingsTab } from "./GlobalSettingsTab";
import "./SettingsView.css";

export const SettingsView: React.FC = () => {
  const { t } = useTranslation();

  return (
    <div className={"settings-view-container"}>
      <div className={"settings-view-content-wrapper"}>
        <Tabs
          defaultActiveKey="profile"
          items={[
            {
              key: "profile",
              label: (
                <>
                  <UserOutlined />
                  {t("settings.tabs.profile")}
                </>
              ),
              children: <ProfileSettingsTab />,
            },
            {
              key: "global",
              label: (
                <>
                  <SettingOutlined />
                  {t("settings.tabs.global")}
                </>
              ),
              children: <GlobalSettingsTab />,
            },
          ]}
        />
      </div>
    </div>
  );
};

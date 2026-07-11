import React from "react";
import { Tabs } from "antd";
import { FolderOutlined, ImportOutlined, SettingOutlined, CloudOutlined, ApiOutlined } from "@ant-design/icons";
import { useTranslation } from "react-i18next";
import { ModWorkSettingsTab } from "./ModWorkSettingsTab";
import { ModImportSettingsTab } from "./ModImportSettingsTab";
import { GlobalSettingsTab } from "./GlobalSettingsTab";
import { OnlineStorageAccountsCard } from "./OnlineStorageAccountsCard";
import { PluginSettingsTab } from "./PluginSettingsTab";
import "./SettingsView.css";

/**
 * Settings shell — one flat tab per concern (the old two-tab layout stacked three growing cards in
 * one scroll page). Each profile tab owns its Save/Reset; Global saves immediately.
 */
export const SettingsView: React.FC = () => {
  const { t } = useTranslation();

  return (
    <div className={"settings-view-container"}>
      <div className={"settings-view-content-wrapper"}>
        {/* Tabs ordered by likelihood of use → advanced/optional last: common app prefs, the
            primary deploy setup, occasional import tuning, then the advanced auth + optional plugin tabs. */}
        <Tabs
          defaultActiveKey="global"
          items={[
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
            {
              key: "modWork",
              label: (
                <>
                  <FolderOutlined />
                  {t("settings.tabs.modWork")}
                </>
              ),
              children: <ModWorkSettingsTab />,
            },
            {
              key: "modImport",
              label: (
                <>
                  <ImportOutlined />
                  {t("settings.tabs.modImport")}
                </>
              ),
              children: <ModImportSettingsTab />,
            },
            {
              key: "onlineStorage",
              label: (
                <>
                  <CloudOutlined />
                  {t("settings.tabs.onlineStorage")}
                </>
              ),
              children: (
                <div className="settings-view-profile">
                  <OnlineStorageAccountsCard />
                </div>
              ),
            },
            {
              key: "plugins",
              label: (
                <>
                  <ApiOutlined />
                  {t("settings.tabs.plugins")}
                </>
              ),
              children: <PluginSettingsTab />,
            },
          ]}
        />
      </div>
    </div>
  );
};

import React, { useEffect } from "react";
import { App as AntdApp } from "antd";
import { setNotificationApi } from "../utils/notification";

/**
 * Component to initialize notification API from AntdApp context
 * This ensures notifications use the correct theme from ConfigProvider
 */
export const NotificationInitializer: React.FC<{
  children: React.ReactNode;
}> = ({ children }) => {
  const { notification: notificationApi } = AntdApp.useApp();

  useEffect(() => {
    // Initialize the notification API singleton
    setNotificationApi(notificationApi);
  }, [notificationApi]);

  return <>{children}</>;
};

import React from 'react';
import CustomNotification from '../components/notification/CustomNotification';
import logger from './logger';

/**
 * Type for Ant Design notification API (from App.useApp hook)
 */
interface NotificationAPI {
  open: (config: {
    title?: React.ReactElement;
    placement?: 'top' | 'topLeft' | 'topRight' | 'bottom' | 'bottomLeft' | 'bottomRight';
    duration?: number;
    className?: string;
    closeIcon?: React.ReactNode | null;
  }) => void;
}

/**
 * Notification API instance - will be injected by NotificationProvider
 * This is set during app initialization to use the context-aware notification API
 */
let notificationApi: NotificationAPI | undefined = undefined;

/**
 * Set the notification API instance (called during app initialization)
 * @internal
 */
export const setNotificationApi = (api: NotificationAPI) => {
  notificationApi = api;
};

/**
 * Wrapper for Ant Design notification API
 * Uses custom React component for message-like appearance with theme support
 * Now uses App.useApp() context to support dynamic theme
 */
export const notification = {
  success: (content: string, duration: number = 2) => {
    if (!notificationApi) {
      logger.error('[notification] API not initialized. Wrap your app with <App> component.');
      return;
    }
    notificationApi.open({
      title: React.createElement(CustomNotification, {
        type: 'success',
        message: content,
      }),
      placement: 'top',
      duration,
      className: 'custom-notification-wrapper',
      closeIcon: null, // Close icon is handled by CustomNotification
    });
  },

  error: (content: string, duration: number = 3) => {
    if (!notificationApi) {
      logger.error('[notification] API not initialized. Wrap your app with <App> component.');
      return;
    }
    notificationApi.open({
      title: React.createElement(CustomNotification, {
        type: 'error',
        message: content,
      }),
      placement: 'top',
      duration,
      className: 'custom-notification-wrapper',
      closeIcon: null,
    });
  },

  info: (content: string, duration: number = 2) => {
    if (!notificationApi) {
      logger.error('[notification] API not initialized. Wrap your app with <App> component.');
      return;
    }
    notificationApi.open({
      title: React.createElement(CustomNotification, {
        type: 'info',
        message: content,
      }),
      placement: 'top',
      duration,
      className: 'custom-notification-wrapper',
      closeIcon: null,
    });
  },

  warning: (content: string, duration: number = 2.5) => {
    if (!notificationApi) {
      logger.error('[notification] API not initialized. Wrap your app with <App> component.');
      return;
    }
    notificationApi.open({
      title: React.createElement(CustomNotification, {
        type: 'warning',
        message: content,
      }),
      placement: 'top',
      duration,
      className: 'custom-notification-wrapper',
      closeIcon: null,
    });
  },

  loading: (content: string) => {
    if (!notificationApi) {
      logger.error('[notification] API not initialized. Wrap your app with <App> component.');
      return;
    }
    notificationApi.open({
      title: React.createElement(CustomNotification, {
        type: 'loading',
        message: content,
      }),
      placement: 'top',
      duration: 0, // Don't auto-close
      className: 'custom-notification-wrapper',
      closeIcon: null,
    });
  },
};

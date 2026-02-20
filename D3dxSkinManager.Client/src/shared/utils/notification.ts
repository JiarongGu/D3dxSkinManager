import React from 'react';
import { notification as antdNotification } from 'antd';
import CustomNotification from '../components/notification/CustomNotification';

/**
 * Wrapper for Ant Design notification API
 * Uses custom React component for message-like appearance with theme support
 */
export const notification = {
  success: (content: string) => {
    antdNotification.open({
      title: React.createElement(CustomNotification, {
        type: 'success',
        message: content,
      }),
      placement: 'top',
      duration: 2,
      className: 'custom-notification-wrapper',
      closeIcon: null, // Close icon is handled by CustomNotification
    });
  },

  error: (content: string) => {
    antdNotification.open({
      title: React.createElement(CustomNotification, {
        type: 'error',
        message: content,
      }),
      placement: 'top',
      duration: 3,
      className: 'custom-notification-wrapper',
      closeIcon: null,
    });
  },

  info: (content: string) => {
    antdNotification.open({
      title: React.createElement(CustomNotification, {
        type: 'info',
        message: content,
      }),
      placement: 'top',
      duration: 2,
      className: 'custom-notification-wrapper',
      closeIcon: null,
    });
  },

  warning: (content: string) => {
    antdNotification.open({
      title: React.createElement(CustomNotification, {
        type: 'warning',
        message: content,
      }),
      placement: 'top',
      duration: 2.5,
      className: 'custom-notification-wrapper',
      closeIcon: null,
    });
  },

  loading: (content: string) => {
    antdNotification.open({
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

// Export raw notification API for advanced use cases
export { antdNotification };

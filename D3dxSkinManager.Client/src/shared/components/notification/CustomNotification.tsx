import React from 'react';
import { CheckCircleOutlined, CloseCircleOutlined, InfoCircleOutlined, ExclamationCircleOutlined, LoadingOutlined } from '@ant-design/icons';
import './CustomNotification.css';

export type NotificationType = 'success' | 'error' | 'info' | 'warning' | 'loading';

export interface CustomNotificationProps {
  type: NotificationType;
  message: string;
  onClose?: () => void;
}

const iconMap = {
  success: CheckCircleOutlined,
  error: CloseCircleOutlined,
  info: InfoCircleOutlined,
  warning: ExclamationCircleOutlined,
  loading: LoadingOutlined,
};

const CustomNotification: React.FC<CustomNotificationProps> = ({ type, message, onClose }) => {
  const Icon = iconMap[type];

  return (
    <div className={`custom-notification custom-notification-${type}`}>
      <div className="custom-notification-content">
        <Icon className="custom-notification-icon" />
        <span className="custom-notification-message">{message}</span>
      </div>
      {onClose && (
        <button className="custom-notification-close" onClick={onClose} aria-label="Close">
          ×
        </button>
      )}
    </div>
  );
};

export default CustomNotification;

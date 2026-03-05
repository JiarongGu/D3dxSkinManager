import React from 'react';
import ReactDOM from 'react-dom/client';
import { ConfigProvider, theme } from 'antd';
import { ScreenCaptureTool } from './modules/tool/components/ScreenCaptureTool/ScreenCaptureTool';
import './index.css';

const CaptureApp: React.FC = () => {
  // Always use dark theme for capture control panel
  return (
    <ConfigProvider
      theme={{
        algorithm: theme.darkAlgorithm,
        token: {
          colorPrimary: '#1890ff',
          borderRadius: 6,
          fontSize: 14,
        },
      }}
    >
      <ScreenCaptureTool />
    </ConfigProvider>
  );
};

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <CaptureApp />
  </React.StrictMode>
);

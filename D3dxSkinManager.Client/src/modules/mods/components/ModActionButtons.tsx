import React from 'react';
import { Button, Space, Tooltip } from 'antd';
import { DeleteOutlined } from '@ant-design/icons';
import { ModInfo } from '../../../shared/types/mod.types';

interface ModActionButtonsProps {
  mod: ModInfo;
  onLoad: (sha: string) => void;
  onUnload: (sha: string) => void;
  onDelete: (sha: string, name: string) => void;
}

export const ModActionButtons: React.FC<ModActionButtonsProps> = ({
  mod,
  onLoad,
  onUnload,
  onDelete
}) => {
  return (
    <Space size="small">
      {mod.isLoaded ? (
        <Button
          size="small"
          danger
          onClick={() => onUnload(mod.sha)}
        >
          Unload
        </Button>
      ) : (
        <Button
          size="small"
          type="primary"
          onClick={() => onLoad(mod.sha)}
          disabled={!mod.isAvailable}
        >
          Load
        </Button>
      )}
      <Tooltip title="Delete mod permanently">
        <Button
          size="small"
          danger
          icon={<DeleteOutlined />}
          onClick={() => onDelete(mod.sha, mod.name)}
        />
      </Tooltip>
    </Space>
  );
};

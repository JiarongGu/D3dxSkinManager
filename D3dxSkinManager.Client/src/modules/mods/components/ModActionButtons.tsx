import React from 'react';
import { Tooltip } from 'antd';
import { DeleteOutlined } from '@ant-design/icons';
import { CompactButton, CompactSpace } from '../../../shared/components/compact';
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
    <CompactSpace size="small">
      {mod.isLoaded ? (
        <CompactButton
          size="small"
          danger
          onClick={() => onUnload(mod.sha)}
        >
          Unload
        </CompactButton>
      ) : (
        <CompactButton
          size="small"
          type="primary"
          onClick={() => onLoad(mod.sha)}
          disabled={!mod.isAvailable}
        >
          Load
        </CompactButton>
      )}
      <Tooltip title="Delete mod permanently">
        <CompactButton
          size="small"
          danger
          icon={<DeleteOutlined />}
          onClick={() => onDelete(mod.sha, mod.name)}
        />
      </Tooltip>
    </CompactSpace>
  );
};

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
    <CompactSpace size="middle">
      {mod.isLoaded ? (
        <CompactButton
          size="medium"
          danger
          onClick={() => onUnload(mod.sha)}
          style={{ minWidth: '80px' }}
        >
          Unload
        </CompactButton>
      ) : (
        <CompactButton
          size="medium"
          type="primary"
          onClick={() => onLoad(mod.sha)}
          disabled={!mod.isAvailable}
          style={{ minWidth: '80px' }}
        >
          Load
        </CompactButton>
      )}
      <Tooltip title="Delete mod permanently">
        <CompactButton
          size="medium"
          danger
          icon={<DeleteOutlined style={{ fontSize: '16px' }} />}
          onClick={() => onDelete(mod.sha, mod.name)}
        />
      </Tooltip>
    </CompactSpace>
  );
};

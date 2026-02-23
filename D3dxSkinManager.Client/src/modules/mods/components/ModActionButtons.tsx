import React from 'react';
import { Tooltip } from 'antd';
import { DeleteOutlined } from '@ant-design/icons';
import { CompactButton, CompactSpace } from '../../../shared/components/compact';
import { ModInfo } from '../../../shared/types/mod.types';
import './ModActionButtons.css';

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
          className="mod-action-button"
        >
          Unload
        </CompactButton>
      ) : (
        <CompactButton
          size="medium"
          type="primary"
          onClick={() => onLoad(mod.sha)}
          disabled={!mod.isAvailable}
          className="mod-action-button"
        >
          Load
        </CompactButton>
      )}
      <Tooltip title="Delete mod permanently">
        <CompactButton
          size="medium"
          danger
          icon={<DeleteOutlined className="mod-action-delete-icon" />}
          onClick={() => onDelete(mod.sha, mod.name)}
        />
      </Tooltip>
    </CompactSpace>
  );
};

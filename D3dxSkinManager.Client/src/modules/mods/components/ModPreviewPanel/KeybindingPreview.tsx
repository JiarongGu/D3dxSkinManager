import React, { useState, useEffect } from 'react';
import { Empty, Spin, Typography } from 'antd';
import { useTranslation } from 'react-i18next';
import { ModKeybinding } from '../../../../shared/types/mod.types';
import { modService } from '../../services/modService';
import { useProfile } from '../../../../shared/context/ProfileContext';
import './KeybindingPreview.css';

const { Text } = Typography;

export interface KeybindingPreviewProps {
  modSha: string;
}

export const KeybindingPreview: React.FC<KeybindingPreviewProps> = ({ modSha }) => {
  const { t } = useTranslation();
  const { state: profileState } = useProfile();
  const selectedProfileId = profileState.selectedProfile?.id;

  const [keybindings, setKeybindings] = useState<ModKeybinding[]>([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    const loadKeybindings = async () => {
      if (!selectedProfileId || !modSha) return;

      setLoading(true);
      try {
        const bindings = await modService.getKeybindings(selectedProfileId, modSha);
        setKeybindings(bindings);
      } catch (error) {
        console.error('Error loading keybindings:', error);
        setKeybindings([]);
      } finally {
        setLoading(false);
      }
    };

    void loadKeybindings();
  }, [selectedProfileId, modSha]);

  if (loading) {
    return (
      <div className="keybinding-preview-loading">
        <Spin size="small" />
      </div>
    );
  }

  if (keybindings.length === 0) {
    return (
      <div className="keybinding-preview-empty">
        <Empty
          description={t('mods.keybindings.noKeybindings')}
          image={Empty.PRESENTED_IMAGE_SIMPLE}
        />
      </div>
    );
  }

  return (
    <div className="keybinding-preview">
      <div className="keybinding-list">
        {keybindings.map((binding, index) => (
          <div key={index} className="keybinding-item">
            <div className="keybinding-key">
              <kbd className="keybinding-kbd">{binding.keyDisplay}</kbd>
            </div>
            <div className="keybinding-description">
              <Text className="keybinding-description-text">{binding.description}</Text>
              {binding.type && (
                <Text type="secondary" className="keybinding-type">
                  {binding.type}
                </Text>
              )}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
};

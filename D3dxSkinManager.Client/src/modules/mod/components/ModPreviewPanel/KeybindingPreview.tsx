import React, { useState, useEffect, useCallback } from 'react';
import { Empty, Spin, Typography, Input, Tooltip } from 'antd';
import { EditOutlined, CheckOutlined, CloseOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { ModKeybinding } from '../../../../shared/types/mod.types';
import { modService } from '../../../../shared/services/ipc';
import { useProfile } from '../../../../shared/context/ProfileContext';
import { CompactButton } from '../../../../shared/components/compact';
import { notification } from '../../../../shared/utils/notification';
import { handleError } from '../../../../shared/utils/errorHandler';
import './KeybindingPreview.css';

const { Text } = Typography;

export interface KeybindingPreviewProps {
  modId: string;
}

/** Map a browser keydown to a 3DMigoto `key =` value (single key; combos can be typed manually). */
function eventToMigotoKey(e: React.KeyboardEvent): string | null {
  const k = e.key;
  if (/^[a-zA-Z0-9]$/.test(k)) return k.toLowerCase();
  if (/^F([1-9]|1[0-2])$/.test(k)) return 'VK_' + k.toUpperCase();
  const map: Record<string, string> = {
    ArrowUp: 'VK_UP', ArrowDown: 'VK_DOWN', ArrowLeft: 'VK_LEFT', ArrowRight: 'VK_RIGHT',
    ' ': 'VK_SPACE', Enter: 'VK_RETURN', Tab: 'VK_TAB', Backspace: 'VK_BACK',
    Delete: 'VK_DELETE', Insert: 'VK_INSERT', Home: 'VK_HOME', End: 'VK_END',
    PageUp: 'VK_PRIOR', PageDown: 'VK_NEXT',
  };
  return map[k] ?? null;
}

export const KeybindingPreview: React.FC<KeybindingPreviewProps> = ({ modId }) => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();

  const [keybindings, setKeybindings] = useState<ModKeybinding[]>([]);
  const [loading, setLoading] = useState(false);
  const [editingKey, setEditingKey] = useState<string | null>(null); // the binding.key being edited
  const [draftKey, setDraftKey] = useState('');
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    if (!selectedProfileId || !modId) return;
    setLoading(true);
    try {
      setKeybindings(await modService.getKeybindings(selectedProfileId, modId));
    } catch {
      setKeybindings([]);
    } finally {
      setLoading(false);
    }
  }, [selectedProfileId, modId]);

  useEffect(() => { void load(); setEditingKey(null); }, [load]);

  const startEdit = (binding: ModKeybinding) => {
    setEditingKey(binding.key);
    setDraftKey(binding.key);
  };

  const cancelEdit = () => { setEditingKey(null); setDraftKey(''); };

  const saveEdit = useCallback(async (oldKey: string) => {
    const newKey = draftKey.trim();
    if (!selectedProfileId || !newKey || newKey === oldKey) { cancelEdit(); return; }
    setSaving(true);
    try {
      await modService.updateKeybinding(selectedProfileId, modId, oldKey, newKey);
      notification.success(t('mods.keybindings.rebound'));
      cancelEdit();
      await load();
    } catch (error) {
      handleError(error);
    } finally {
      setSaving(false);
    }
  }, [selectedProfileId, modId, draftKey, t, load]);

  if (loading) {
    return <div className="keybinding-preview-loading"><Spin size="small" /></div>;
  }

  if (keybindings.length === 0) {
    return (
      <div className="keybinding-preview-empty">
        <Empty description={t('mods.keybindings.noKeybindings')} image={Empty.PRESENTED_IMAGE_SIMPLE} />
      </div>
    );
  }

  return (
    <div className="keybinding-preview">
      <div className="keybinding-list">
        {keybindings.map((binding, index) => {
          const editing = editingKey === binding.key;
          return (
            <div key={index} className="keybinding-item">
              <div className="keybinding-key">
                {editing ? (
                  <Input
                    autoFocus
                    size="small"
                    value={draftKey}
                    placeholder={t('mods.keybindings.pressKey')}
                    onChange={(e) => setDraftKey(e.target.value)}
                    onKeyDown={(e) => {
                      if (e.key === 'Escape') { cancelEdit(); return; }
                      if (e.key === 'Enter') { void saveEdit(binding.key); return; }
                      const mapped = eventToMigotoKey(e);
                      if (mapped) { e.preventDefault(); setDraftKey(mapped); }
                    }}
                    style={{ width: 120 }}
                  />
                ) : (
                  <kbd className="keybinding-kbd">{binding.keyDisplay}</kbd>
                )}
              </div>
              <div className="keybinding-description">
                <Text className="keybinding-description-text">{binding.description}</Text>
                {binding.type && <Text type="secondary" className="keybinding-type">{binding.type}</Text>}
              </div>
              <div className="keybinding-actions">
                {editing ? (
                  <>
                    <CompactButton size="small" type="text" icon={<CheckOutlined />} loading={saving} onClick={() => saveEdit(binding.key)} />
                    <CompactButton size="small" type="text" icon={<CloseOutlined />} onClick={cancelEdit} />
                  </>
                ) : (
                  <Tooltip title={t('mods.keybindings.rebind')}>
                    <CompactButton size="small" type="text" icon={<EditOutlined />} onClick={() => startEdit(binding)} />
                  </Tooltip>
                )}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
};

import React, { useState, useEffect, useCallback, useRef } from 'react';
import { Empty, Spin, Typography, Input, Tooltip } from 'antd';
import { EditOutlined, CheckOutlined, CloseOutlined, HolderOutlined } from '@ant-design/icons';
import classNames from 'classnames';
import { useTranslation } from 'react-i18next';
import { ModKeybinding } from '../../../../shared/types/mod.types';
import { modService } from '../../../../shared/services/ipc';
import { useProfile } from '../../../../shared/context/ProfileContext';
import { CompactIconButton } from '../../../../shared/components/compact';
import { notification } from '../../../../shared/utils/notification';
import { handleError } from '../../../../shared/utils/errorHandler';
import './KeybindingPreview.css';

const { Text } = Typography;

export interface KeybindingPreviewProps {
  modId: string;
}

const VK_MAP: Record<string, string> = {
  ArrowUp: 'VK_UP', ArrowDown: 'VK_DOWN', ArrowLeft: 'VK_LEFT', ArrowRight: 'VK_RIGHT',
  ' ': 'VK_SPACE', Enter: 'VK_RETURN', Tab: 'VK_TAB', Backspace: 'VK_BACK',
  Delete: 'VK_DELETE', Insert: 'VK_INSERT', Home: 'VK_HOME', End: 'VK_END',
  PageUp: 'VK_PRIOR', PageDown: 'VK_NEXT',
};
const VK_DISPLAY: Record<string, string> = {
  VK_UP: '↑', VK_DOWN: '↓', VK_LEFT: '←', VK_RIGHT: '→', VK_SPACE: 'Space', VK_RETURN: 'Enter',
  VK_TAB: 'Tab', VK_BACK: 'Backspace', VK_DELETE: 'Del', VK_INSERT: 'Ins', VK_HOME: 'Home',
  VK_END: 'End', VK_PRIOR: 'PgUp', VK_NEXT: 'PgDn',
};

interface Chord { base: string; ctrl: boolean; shift: boolean; alt: boolean; }

/** The non-modifier base key for a 3DMigoto binding, or null while only modifiers are held. */
function baseFromKey(key: string): string | null {
  if (key === 'Control' || key === 'Alt' || key === 'Shift' || key === 'Meta') return null;
  if (/^[a-zA-Z0-9]$/.test(key)) return key.toLowerCase();
  if (/^F([1-9]|1[0-2])$/.test(key)) return 'VK_' + key.toUpperCase();
  return VK_MAP[key] ?? null;
}

/**
 * Raw 3DMigoto value. Unheld modifiers default to `no_ctrl`/`no_shift`/`no_alt` so a plain key won't
 * also fire when another binding's modifiers are held (precise, non-overlapping). e.g. "j" →
 * "no_ctrl no_shift no_alt j"; Ctrl+J → "ctrl no_shift no_alt j".
 */
function buildRaw(c: Chord): string {
  return [c.ctrl ? 'ctrl' : 'no_ctrl', c.shift ? 'shift' : 'no_shift', c.alt ? 'alt' : 'no_alt', c.base].join(' ');
}

/** Friendly display of the captured chord (active modifiers only). e.g. "Ctrl + J", "F5". */
function buildDisplay(c: Chord): string {
  const parts: string[] = [];
  if (c.ctrl) parts.push('Ctrl');
  if (c.shift) parts.push('Shift');
  if (c.alt) parts.push('Alt');
  parts.push(c.base.startsWith('VK_') ? (VK_DISPLAY[c.base] ?? c.base.replace('VK_', '')) : c.base.toUpperCase());
  return parts.join(' + ');
}

export const KeybindingPreview: React.FC<KeybindingPreviewProps> = ({ modId }) => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();

  const [keybindings, setKeybindings] = useState<ModKeybinding[]>([]);
  const [loading, setLoading] = useState(false);
  const [editingKey, setEditingKey] = useState<string | null>(null); // the binding.key being edited
  const [draftDisplay, setDraftDisplay] = useState('');               // friendly text shown in the field
  const [recording, setRecording] = useState(false);                  // field focused, listening for keys
  const [saving, setSaving] = useState(false);
  const [dragIndex, setDragIndex] = useState<number | null>(null);
  const [dropIndex, setDropIndex] = useState<number | null>(null); // insertion slot (0..length)
  const draftRaw = useRef('');                  // the 3DMigoto value to save (with no_ defaults)
  const held = useRef<Set<string>>(new Set());  // currently-pressed key codes (for "until all released")

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
    draftRaw.current = binding.key;
    setDraftDisplay(binding.keyDisplay || binding.key);
    held.current.clear();
  };

  const cancelEdit = () => { setEditingKey(null); setDraftDisplay(''); draftRaw.current = ''; setRecording(false); held.current.clear(); };

  // Capture a chord: accumulate held keys; the latest non-modifier press + its modifier flags is the
  // binding. Value updates live as the chord builds; releasing all keys just locks it in.
  const onCaptureKeyDown = (e: React.KeyboardEvent) => {
    e.preventDefault();
    held.current.add(e.code);
    const base = baseFromKey(e.key);
    if (!base) return; // only a modifier so far — keep waiting
    const chord: Chord = { base, ctrl: e.ctrlKey, shift: e.shiftKey, alt: e.altKey };
    draftRaw.current = buildRaw(chord);
    setDraftDisplay(buildDisplay(chord));
  };
  const onCaptureKeyUp = (e: React.KeyboardEvent) => {
    e.preventDefault();
    held.current.delete(e.code);
    // value is already set on keydown; once all keys are released the chord is final (no-op here).
  };

  const saveEdit = useCallback(async (oldKey: string) => {
    const newKey = draftRaw.current.trim();
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
  }, [selectedProfileId, modId, t, load]);

  // Persist a new order by sending the keys top-to-bottom; backend permutes the [Key*] blocks.
  const persistOrder = useCallback(async (items: ModKeybinding[]) => {
    if (!selectedProfileId) return;
    try {
      await modService.reorderKeybindings(selectedProfileId, modId, items.map((b) => b.key));
    } catch (error) {
      handleError(error);
      void load(); // revert to the on-disk order on failure
    }
  }, [selectedProfileId, modId, load]);

  const handleDrop = () => {
    const from = dragIndex;
    const insertAt = dropIndex;
    setDragIndex(null);
    setDropIndex(null);
    if (from === null || insertAt === null) return;
    const idx = from < insertAt ? insertAt - 1 : insertAt; // account for removal of the dragged item
    if (idx === from) return;
    const items = [...keybindings];
    const [moved] = items.splice(from, 1);
    items.splice(idx, 0, moved);
    setKeybindings(items); // optimistic
    void persistOrder(items);
  };

  // Which slot the dragged row will land in: before this row, or after it (bottom half).
  const onRowDragOver = (index: number) => (e: React.DragEvent) => {
    e.preventDefault();
    const r = e.currentTarget.getBoundingClientRect();
    setDropIndex(e.clientY > r.top + r.height / 2 ? index + 1 : index);
  };

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
    // Stop clicks (rebind button, inputs) from bubbling to the overlay backdrop's close handler.
    <div className="keybinding-preview" onClick={(e) => e.stopPropagation()}>
      <div className="keybinding-list">
        {keybindings.map((binding, index) => {
          const editing = editingKey === binding.key;
          return (
            <React.Fragment key={binding.key}>
              {dropIndex === index && dragIndex !== null && <div className="keybinding-drop-line" />}
              <div
                className={classNames('keybinding-item', { 'keybinding-item--dragging': dragIndex === index })}
                draggable={!editing}
                onDragStart={(e) => {
                  // setData is required for HTML5 DnD to actually initiate the drag (else dragover/drop
                  // never fire); mirrors the category card drag.
                  e.dataTransfer.effectAllowed = 'move';
                  e.dataTransfer.setData('text/plain', String(index));
                  setDragIndex(index);
                }}
                onDragOver={onRowDragOver(index)}
                onDrop={(e) => { e.preventDefault(); handleDrop(); }}
                onDragEnd={() => { setDragIndex(null); setDropIndex(null); }}
              >
              <span className="keybinding-drag-handle" title={t('mods.keybindings.reorder')}>
                <HolderOutlined />
              </span>
              <div className="keybinding-key">
                {editing ? (
                  <Input
                    autoFocus
                    readOnly
                    size="small"
                    className={classNames('keybinding-capture', { 'keybinding-capture--recording': recording })}
                    value={draftDisplay}
                    placeholder={t('mods.keybindings.pressKey')}
                    onFocus={() => { setRecording(true); held.current.clear(); }}
                    onBlur={() => setRecording(false)}
                    onKeyDown={onCaptureKeyDown}
                    onKeyUp={onCaptureKeyUp}
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
                    <CompactIconButton
                      tone="success"
                      icon={<CheckOutlined />}
                      loading={saving}
                      onMouseDown={(e) => e.preventDefault()}
                      onClick={() => saveEdit(binding.key)}
                      title={t('common.save')}
                    />
                    <CompactIconButton
                      tone="danger"
                      icon={<CloseOutlined />}
                      onMouseDown={(e) => e.preventDefault()}
                      onClick={cancelEdit}
                      title={t('common.cancel')}
                    />
                  </>
                ) : (
                  <Tooltip title={t('mods.keybindings.rebind')}>
                    <CompactIconButton icon={<EditOutlined />} onClick={() => startEdit(binding)} />
                  </Tooltip>
                )}
              </div>
              </div>
            </React.Fragment>
          );
        })}
        {dropIndex === keybindings.length && dragIndex !== null && <div className="keybinding-drop-line" />}
      </div>
    </div>
  );
};

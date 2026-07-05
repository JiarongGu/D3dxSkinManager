import React, { useState, useEffect, useCallback, useRef } from 'react';
import { Empty, Spin, Typography, Tooltip, Space } from 'antd';
import { EditOutlined, CheckOutlined, CloseOutlined, HolderOutlined } from '@ant-design/icons';
import classNames from 'classnames';
import { useTranslation } from 'react-i18next';
import { ModKeybinding } from '../../../../shared/types/mod.types';
import { modService } from '../../../../shared/services/ipc';
import { useProfile } from '../../../../shared/context/ProfileContext';
import { CompactIconButton, CompactInput } from '../../../../shared/components/compact';
import { notification } from '../../../../shared/utils/notification';
import { handleError } from '../../../../shared/utils/errorHandler';
import { Chord, baseFromEvent, buildRaw, buildDisplay, rawToDisplay } from '../../../../shared/utils/keyChord';
import { XboxButtonPicker } from '../../../../shared/components/common/XboxButtonPicker';
import './KeybindingPreview.css';

const { Text } = Typography;

export interface KeybindingPreviewProps {
  modId: string;
}

export const KeybindingPreview: React.FC<KeybindingPreviewProps> = ({ modId }) => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();

  const [keybindings, setKeybindings] = useState<ModKeybinding[]>([]);
  const [loading, setLoading] = useState(false);
  // The specific raw chord being rebound — a row can carry SEVERAL `key =` chords (keyboard +
  // controller alternates), each independently editable, so track the raw value, not just the row.
  const [editingRaw, setEditingRaw] = useState<string | null>(null);
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

  useEffect(() => { void load(); setEditingRaw(null); }, [load]);

  const startEdit = (rawKey: string, display: string) => {
    setEditingRaw(rawKey);
    draftRaw.current = rawKey;
    setDraftDisplay(display || rawKey);
    held.current.clear();
  };

  const cancelEdit = () => { setEditingRaw(null); setDraftDisplay(''); draftRaw.current = ''; setRecording(false); held.current.clear(); };

  // Capture a chord: accumulate held keys; the latest non-modifier press + its modifier flags is the
  // binding. Value updates live as the chord builds; releasing all keys just locks it in.
  // Base from e.code (layout/shift-independent) — e.key made digit/symbol combos uncapturable.
  const onCaptureKeyDown = (e: React.KeyboardEvent) => {
    e.preventDefault();
    held.current.add(e.code);
    const base = baseFromEvent(e.code, e.key);
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

  // Drop target is the WHOLE list (not each row), so dropping in the gaps/padding between rows still
  // works — find the insertion slot from the cursor's Y vs each row's midpoint.
  const onListDragOver = (e: React.DragEvent) => {
    if (dragIndex === null) return;
    e.preventDefault();
    e.dataTransfer.dropEffect = 'move';
    const rows = Array.from(
      e.currentTarget.querySelectorAll<HTMLElement>('.keybinding-item'),
    );
    let slot = rows.length; // past the last row → append
    for (let i = 0; i < rows.length; i++) {
      const r = rows[i].getBoundingClientRect();
      if (e.clientY < r.top + r.height / 2) { slot = i; break; }
    }
    setDropIndex(slot);
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
      <div
        className="keybinding-list"
        onDragOver={onListDragOver}
        onDrop={(e) => { e.preventDefault(); handleDrop(); }}
      >
        {keybindings.map((binding, index) => {
          // Every chord of the row (primary + keyboard/controller alternates), each rebindable.
          const chords: { raw: string; display: string }[] = [
            { raw: binding.key, display: binding.keyDisplay },
            ...(binding.additionalKeys ?? []).map((raw, i) => ({
              raw,
              display: binding.additionalKeyDisplays?.[i] ?? raw,
            })),
          ];
          const editing = chords.some((c) => editingRaw === c.raw);
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
                onDragEnd={() => { setDragIndex(null); setDropIndex(null); }}
              >
              <span className="keybinding-drag-handle" title={t('mods.keybindings.reorder')}>
                <HolderOutlined />
              </span>
              <div className="keybinding-key">
                {chords.map((chord) =>
                  editingRaw === chord.raw ? (
                    <Space.Compact key={chord.raw} className="keybinding-capture-group">
                      <CompactInput
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
                      <XboxButtonPicker
                        onPick={(raw) => { draftRaw.current = raw; setDraftDisplay(rawToDisplay(raw)); }}
                      />
                    </Space.Compact>
                  ) : (
                    <Tooltip key={chord.raw} title={t('mods.keybindings.rebind')}>
                      <kbd
                        className={classNames('keybinding-kbd', { 'keybinding-kbd--locked': editing })}
                        onClick={() => { if (!editing) startEdit(chord.raw, chord.display); }}
                      >
                        {chord.display}
                      </kbd>
                    </Tooltip>
                  ),
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
                      onClick={() => { if (editingRaw) void saveEdit(editingRaw); }}
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
                    <CompactIconButton icon={<EditOutlined />} onClick={() => startEdit(binding.key, binding.keyDisplay)} />
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

import React, { useState, useEffect, useCallback, useRef } from 'react';
import { Empty, Spin, Typography, Tooltip } from 'antd';
import { EditOutlined, CheckOutlined, CloseOutlined, HolderOutlined, PlusOutlined } from '@ant-design/icons';
import classNames from 'classnames';
import { useTranslation } from 'react-i18next';
import { ModKeybinding } from '../../../../shared/types/mod.types';
import { modService } from '../../../../shared/services/ipc';
import { useProfile } from '../../../../shared/context/ProfileContext';
import { CompactIconButton, CompactButton } from '../../../../shared/components/compact';
import { KeyCaptureInput } from '../../../../shared/components/common/KeyCaptureInput';
import { notification } from '../../../../shared/utils/notification';
import { handleError } from '../../../../shared/utils/errorHandler';
import { isControllerRaw } from '../../../../shared/utils/keyChord';
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
  const [saving, setSaving] = useState(false);
  // Row EDIT MODE — one binding at a time. On edit, ALL its keys become editable KeyCaptureInputs and an
  // "add key" button appears; rebind / add / remove all happen here and Save writes the whole key set in
  // one atomic op (setKeybindingKeys). This is the consistent alternative to the old mixed chips+one-field.
  const [editingBindingKey, setEditingBindingKey] = useState<string | null>(null);
  const [editKeys, setEditKeys] = useState<{ id: number; raw: string }[]>([]);
  const nextKeyId = useRef(0);
  const [dragIndex, setDragIndex] = useState<number | null>(null);
  const [dropIndex, setDropIndex] = useState<number | null>(null); // insertion slot (0..length)

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

  useEffect(() => { void load(); setEditingBindingKey(null); }, [load]);

  // Enter edit mode: seed the working set from the binding's current keys (primary + alternates).
  const startRowEdit = (binding: ModKeybinding) => {
    const chords = [binding.key, ...(binding.additionalKeys ?? [])];
    setEditKeys(chords.map((raw, i) => ({ id: i, raw })));
    nextKeyId.current = chords.length;
    setEditingBindingKey(binding.key);
  };
  const cancelRowEdit = () => { setEditingBindingKey(null); setEditKeys([]); };
  const updateEditKey = (id: number, raw: string) => setEditKeys((p) => p.map((k) => (k.id === id ? { ...k, raw } : k)));
  const removeEditKey = (id: number) => setEditKeys((p) => (p.length > 1 ? p.filter((k) => k.id !== id) : p));
  const addEditKey = () => setEditKeys((p) => [...p, { id: nextKeyId.current++, raw: '' }]);

  // Save the whole edited set at once — anchorKey is the binding's ORIGINAL primary (still on disk).
  const saveRowEdit = useCallback(async (anchorKey: string) => {
    const keys = editKeys.map((k) => k.raw.trim()).filter(Boolean);
    if (!selectedProfileId || keys.length === 0) { cancelRowEdit(); return; }
    setSaving(true);
    try {
      await modService.setKeybindingKeys(selectedProfileId, modId, anchorKey, keys);
      notification.success(t('mods.keybindings.rebound'));
      cancelRowEdit();
      await load();
    } catch (error) {
      handleError(error);
    } finally {
      setSaving(false);
    }
  }, [selectedProfileId, modId, editKeys, t, load]);

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
          const allChords: { raw: string; display: string }[] = [
            { raw: binding.key, display: binding.keyDisplay },
            ...(binding.additionalKeys ?? []).map((raw, i) => ({
              raw,
              display: binding.additionalKeyDisplays?.[i] ?? raw,
            })),
          ];
          const keyboardChords = allChords.filter((c) => !isControllerRaw(c.raw));
          const controllerChords = allChords.filter((c) => isControllerRaw(c.raw));
          const editing = editingBindingKey === binding.key;

          if (editing) {
            // EDIT MODE: every key (keyboard + controller) is a KeyCaptureInput + an "Add key" button —
            // all consistent, one Save writes the whole set. See setKeybindingKeys.
            const noKeys = editKeys.every((k) => !k.raw.trim());
            return (
              <React.Fragment key={binding.key}>
                {dropIndex === index && dragIndex !== null && <div className="keybinding-drop-line" />}
                <div className="keybinding-item keybinding-item--editing">
                  {/* No drag handle in edit mode (can't reorder while editing). A standalone editor: a
                      label header, then the key fields with room, then Add-key. */}
                  <div className="keybinding-edit">
                    <div className="keybinding-edit-head">
                      <span className="keybinding-edit-name">{binding.description || binding.key}</span>
                      {binding.type && <span className="keybinding-edit-type">{binding.type}</span>}
                    </div>
                    <div className="keybinding-edit-keys">
                      {editKeys.map((k, idx) => {
                        // A per-field delete (×) sits after EVERY field. On the LAST remaining key it's
                        // disabled (dimmed) — removing a key must never delete the whole binding.
                        const canRemove = editKeys.length > 1;
                        return (
                          <div key={k.id} className="keybinding-edit-key">
                            <KeyCaptureInput
                              className="keybinding-capture"
                              autoFocus={k.raw === '' && idx === editKeys.length - 1}
                              value={k.raw}
                              onChange={(raw) => updateEditKey(k.id, raw)}
                            />
                            <button
                              type="button"
                              className="keybinding-mini-x"
                              title={canRemove ? t('mods.keybindings.removeAlternate') : t('mods.keybindings.removeKeyLast')}
                              tabIndex={canRemove ? 0 : -1}
                              disabled={!canRemove}
                              onMouseDown={(e) => e.preventDefault()}
                              onClick={() => { if (canRemove) removeEditKey(k.id); }}
                            >
                              <CloseOutlined />
                            </button>
                          </div>
                        );
                      })}
                      <CompactButton
                        size="small"
                        className="keybinding-add-btn"
                        icon={<PlusOutlined />}
                        onMouseDown={(e) => e.preventDefault()}
                        onClick={addEditKey}
                      >
                        {t('mods.keybindings.addKey')}
                      </CompactButton>
                    </div>
                  </div>
                  <div className="keybinding-actions keybinding-actions--edit">
                    <Tooltip title={t('common.save')}>
                      <CompactIconButton
                        tone="success"
                        icon={<CheckOutlined />}
                        loading={saving}
                        disabled={noKeys}
                        onMouseDown={(e) => e.preventDefault()}
                        onClick={() => void saveRowEdit(binding.key)}
                      />
                    </Tooltip>
                    <Tooltip title={t('common.cancel')}>
                      <CompactIconButton
                        tone="danger"
                        icon={<CloseOutlined />}
                        onMouseDown={(e) => e.preventDefault()}
                        onClick={cancelRowEdit}
                      />
                    </Tooltip>
                  </div>
                </div>
              </React.Fragment>
            );
          }

          // VIEW MODE: chips are display-only; the pencil opens edit mode (where everything is editable).
          return (
            <React.Fragment key={binding.key}>
              {dropIndex === index && dragIndex !== null && <div className="keybinding-drop-line" />}
              <div
                className={classNames('keybinding-item', { 'keybinding-item--dragging': dragIndex === index })}
                draggable
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
                  {keyboardChords.map((chord) => (
                    <kbd key={chord.raw} className="keybinding-kbd keybinding-kbd--static">{chord.display}</kbd>
                  ))}
                  {keyboardChords.length === 0 && <span className="keybinding-kbd keybinding-kbd--none">—</span>}
                </div>
                <div className="keybinding-description">
                  <Text className="keybinding-description-text">{binding.description}</Text>
                  {(binding.type || controllerChords.length > 0) && (
                    <div className="keybinding-meta">
                      {binding.type && <Text type="secondary" className="keybinding-type">{binding.type}</Text>}
                      {controllerChords.map((c) => (
                        <Tooltip key={c.raw} title={t('mods.keybindings.controllerHint')}>
                          <span className="keybinding-controller-chip">{c.display}</span>
                        </Tooltip>
                      ))}
                    </div>
                  )}
                </div>
                <div className="keybinding-actions">
                  <Tooltip title={t('mods.keybindings.edit')}>
                    <CompactIconButton icon={<EditOutlined />} onClick={() => startRowEdit(binding)} />
                  </Tooltip>
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

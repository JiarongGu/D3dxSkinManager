import React, { useState, useEffect } from 'react';
import { Modal, Input, Switch, Tooltip, Typography } from 'antd';
import { ArrowUpOutlined, ArrowDownOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { useProfile } from '../../../../shared/context/ProfileContext';
import { modService } from '../../../../shared/services/ipc';
import { notification } from '../../../../shared/utils/notification';
import { handleError } from '../../../../shared/utils/errorHandler';
import { CompactIconButton } from '../../../../shared/components/compact';
import type { ModInfo } from '../../../../shared/types/mod.types';
import './MergeModsDialog.css';

const { Text } = Typography;

interface MergeModsDialogProps {
  visible: boolean;
  mods: ModInfo[];        // the mods to merge (initial swap order)
  onClose: () => void;
}

/**
 * Combine several mods of one slot into a single new mod that cycles between them with one key
 * (GIMI-style). The order is the swap order (top = the variant shown first). Originals are left
 * untouched; a brand-new merged mod is created.
 */
export const MergeModsDialog: React.FC<MergeModsDialogProps> = ({ visible, mods, onClose }) => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();

  const [ordered, setOrdered] = useState<ModInfo[]>(mods);
  const [name, setName] = useState('');
  const [key, setKey] = useState('');
  const [activeOnly, setActiveOnly] = useState(true);
  const [busy, setBusy] = useState(false);

  // Reset the form whenever the dialog (re)opens with a new selection.
  useEffect(() => {
    if (visible) {
      setOrdered(mods);
      setName(mods.length > 0 ? `${mods[0].name} (merged)` : 'Merged');
      setKey('');
      setActiveOnly(true);
    }
  }, [visible, mods]);

  const move = (index: number, delta: number) => {
    const target = index + delta;
    if (target < 0 || target >= ordered.length) return;
    const next = [...ordered];
    [next[index], next[target]] = [next[target], next[index]];
    setOrdered(next);
  };

  const canMerge = ordered.length >= 2 && name.trim().length > 0 && key.trim().length === 1 && !busy;

  const handleMerge = async () => {
    if (!selectedProfileId || !canMerge) return;
    setBusy(true);
    try {
      // Fire-and-forget — the merge runs in the background (tracked in the Activity panel) so the user
      // keeps working; the new mod appears when it finishes. Just start it + close.
      await modService.mergeMods(
        selectedProfileId,
        ordered.map((m) => m.id),
        name.trim(),
        key.trim(),
        activeOnly,
      );
      notification.success(t('mods.merge.started', { name: name.trim() }));
      onClose();
    } catch (error) {
      handleError(error);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal
      open={visible}
      title={t('mods.merge.title')}
      onCancel={busy ? undefined : onClose}
      onOk={() => void handleMerge()}
      okText={t('mods.merge.action')}
      okButtonProps={{ disabled: !canMerge, loading: busy }}
      cancelButtonProps={{ disabled: busy }}
      width={460}
      destroyOnClose
    >
      <p className="merge-dialog__hint">{t('mods.merge.hint')}</p>

      <div className="merge-dialog__label">{t('mods.merge.order')}</div>
      <div className="merge-dialog__list">
        {ordered.map((mod, i) => (
          <div key={mod.id} className="merge-dialog__row">
            <span className="merge-dialog__index">{i + 1}</span>
            <span className="merge-dialog__name" title={mod.name}>{mod.name}</span>
            <span className="merge-dialog__actions">
              <CompactIconButton
                icon={<ArrowUpOutlined />}
                disabled={i === 0 || busy}
                title={t('common.moveUp')}
                onClick={() => move(i, -1)}
              />
              <CompactIconButton
                icon={<ArrowDownOutlined />}
                disabled={i === ordered.length - 1 || busy}
                title={t('common.moveDown')}
                onClick={() => move(i, 1)}
              />
            </span>
          </div>
        ))}
      </div>

      <div className="merge-dialog__field">
        <span className="merge-dialog__field-label">{t('mods.merge.name')}</span>
        <Input size="small" value={name} disabled={busy} onChange={(e) => setName(e.target.value)} maxLength={80} />
      </div>

      <div className="merge-dialog__field">
        <span className="merge-dialog__field-label">{t('mods.merge.key')}</span>
        <Input
          size="small"
          className="merge-dialog__key"
          value={key}
          disabled={busy}
          maxLength={1}
          placeholder={t('mods.merge.keyPlaceholder')}
          onChange={(e) => setKey(e.target.value.slice(0, 1))}
        />
        <Text type="secondary" className="merge-dialog__key-hint">{t('mods.merge.keyHint')}</Text>
      </div>

      <div className="merge-dialog__field">
        <span className="merge-dialog__field-label">
          <Tooltip title={t('mods.merge.activeOnlyHint')}>{t('mods.merge.activeOnly')}</Tooltip>
        </span>
        <Switch size="small" checked={activeOnly} disabled={busy} onChange={setActiveOnly} />
      </div>
    </Modal>
  );
};

import React, { useState, useEffect, useCallback } from 'react';
import { Spin } from 'antd';
import { useTranslation } from 'react-i18next';
import { ConfirmDialog } from '../../../../shared/components/dialogs/ConfirmDialog';
import { modService } from '../../../../shared/services/ipc';
import { useProfile } from '../../../../shared/context/ProfileContext';
import { notification } from '../../../../shared/utils/notification';
import { handleError } from '../../../../shared/utils/errorHandler';
import { formatBytes } from '../../../../shared/utils/formatBytes';
import type { ModOptimizeScanResult } from '../../../../shared/types/mod.types';
import './ModOptimizeDialog.css';

export interface ModOptimizeDialogProps {
  visible: boolean;
  modId?: string;
  modName?: string;
  onClose: () => void;
}

/**
 * Duplicate-asset optimizer for one mod: scans on open (read-only), shows what would be removed
 * (canonical kept, `filename =` refs rewritten), and applies fire-and-forget on confirm — progress
 * lands in the Activity panel. Merged/multi-variant mods often carry identical textures repeatedly.
 */
export const ModOptimizeDialog: React.FC<ModOptimizeDialogProps> = ({ visible, modId, modName, onClose }) => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();

  const [scanning, setScanning] = useState(false);
  const [scan, setScan] = useState<ModOptimizeScanResult | undefined>(undefined);

  useEffect(() => {
    if (!visible || !selectedProfileId || !modId) { setScan(undefined); return; }
    let cancelled = false;
    const run = async () => {
      setScanning(true);
      try {
        const result = await modService.optimizeScan(selectedProfileId, modId);
        if (!cancelled) setScan(result);
      } catch (error: unknown) {
        if (!cancelled) { handleError(error); onClose(); }
      } finally {
        if (!cancelled) setScanning(false);
      }
    };
    void run();
    return () => { cancelled = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [visible, selectedProfileId, modId]);

  const hasDuplicates = (scan?.groups.length ?? 0) > 0;

  const handleApply = useCallback(async () => {
    if (!selectedProfileId || !modId) return;
    if (!hasDuplicates) { onClose(); return; }
    // Immediate ack — the dedup + recompress runs in the background (Activity panel).
    await modService.optimizeApply(selectedProfileId, modId);
    notification.info(t('mods.optimize.started', { name: modName ?? modId }));
    onClose();
  }, [selectedProfileId, modId, hasDuplicates, modName, t, onClose]);

  const content = scanning || !scan ? (
    <div className="mod-optimize__scanning">
      <Spin size="small" />
      <span>{t('mods.optimize.scanning')}</span>
    </div>
  ) : !hasDuplicates ? (
    <div className="mod-optimize__clean">{t('mods.optimize.noDuplicates', { count: scan.totalFiles })}</div>
  ) : (
    <div className="mod-optimize">
      <div className="mod-optimize__summary">
        {t('mods.optimize.summary', {
          groups: scan.groups.length,
          files: scan.groups.reduce((n, g) => n + g.duplicates.length, 0),
          size: formatBytes(scan.wastedBytes),
        })}
      </div>
      <div className="mod-optimize__groups">
        {scan.groups.map((g) => (
          <div key={g.canonical} className="mod-optimize__group">
            <div className="mod-optimize__canonical">
              <code>{g.canonical}</code>
              <span className="mod-optimize__size">{formatBytes(g.sizeBytes)}</span>
            </div>
            {g.duplicates.map((d) => (
              <div key={d} className="mod-optimize__duplicate">
                <code>{d}</code>
              </div>
            ))}
          </div>
        ))}
      </div>
      <div className="mod-optimize__hint">{t('mods.optimize.hint')}</div>
    </div>
  );

  return (
    <ConfirmDialog
      visible={visible}
      title={t('mods.optimize.title', { name: modName ?? modId ?? '' })}
      content={content}
      okText={hasDuplicates ? t('mods.optimize.apply') : t('common.ok')}
      cancelText={t('common.cancel')}
      onOk={handleApply}
      onCancel={onClose}
    />
  );
};

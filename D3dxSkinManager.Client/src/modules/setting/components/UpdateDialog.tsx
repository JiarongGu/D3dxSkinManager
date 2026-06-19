/**
 * UpdateDialog — app self-update screen.
 *
 * Two entry points:
 *  - Manual: opened from Settings with no `prefetched` → it runs the check itself (shows a spinner).
 *  - Auto (startup): App.tsx checks silently and opens this with `prefetched` set → no second call.
 *
 * "Download" opens the GitHub release page in the browser (self-replace is intentionally deferred —
 * see UpdateService). Built on the shared FormDialog (no-blink theming baked in).
 */

import React, { useState, useEffect, useCallback } from "react";
import { Spin } from "antd";
import {
  CheckCircleFilled,
  RocketOutlined,
  DownloadOutlined,
  ArrowRightOutlined,
  CloseCircleOutlined,
} from "@ant-design/icons";
import { useTranslation } from "react-i18next";
import { FormDialog } from "../../../shared/components/dialogs/FormDialog";
import { CompactButton, CompactSpace } from "../../../shared/components/compact";
import { systemService, UpdateInfo } from "../../../shared/services/ipc";
import { handleError } from "../../../shared/utils/errorHandler";
import { logger } from "../../../shared/utils/logger";
import { formatBytes } from "../../../shared/utils/formatBytes";
import "./UpdateDialog.css";

interface UpdateDialogProps {
  open: boolean;
  onClose: () => void;
  /** Pre-fetched result (auto-check on startup). When set, no check is run on open. */
  prefetched?: UpdateInfo;
}

export const UpdateDialog: React.FC<UpdateDialogProps> = ({ open, onClose, prefetched }) => {
  const { t } = useTranslation();
  const [loading, setLoading] = useState(false);
  const [info, setInfo] = useState<UpdateInfo | undefined>(prefetched);
  const [failed, setFailed] = useState(false);

  const runCheck = useCallback(async () => {
    setFailed(false);
    setLoading(true);
    try {
      const result = await systemService.checkForUpdate();
      setInfo(result);
    } catch (error: unknown) {
      logger.error("[UpdateDialog] Update check failed:", error);
      setFailed(true);
    } finally {
      setLoading(false);
    }
  }, []);

  // On open: use prefetched info if given, else run a fresh check.
  useEffect(() => {
    if (!open) return;
    if (prefetched) {
      setInfo(prefetched);
      setFailed(false);
      setLoading(false);
    } else {
      void runCheck();
    }
  }, [open, prefetched, runCheck]);

  const handleDownload = useCallback(async () => {
    if (!info?.releaseUrl) return;
    try {
      await systemService.openUrl(info.releaseUrl);
      onClose();
    } catch (error: unknown) {
      handleError(error);
    }
  }, [info, onClose]);

  const updateAvailable = !!info?.updateAvailable;

  // Footer changes by state: available → Download + Later; otherwise just Close.
  const footer = (
    <CompactSpace className="update-dialog__footer">
      {updateAvailable ? (
        <>
          <CompactButton onClick={onClose}>{t("update.later")}</CompactButton>
          <CompactButton type="primary" icon={<DownloadOutlined />} onClick={handleDownload}>
            {t("update.download")}
          </CompactButton>
        </>
      ) : (
        <CompactButton type="primary" onClick={onClose}>
          {t("update.close")}
        </CompactButton>
      )}
    </CompactSpace>
  );

  return (
    <FormDialog
      visible={open}
      title={t("update.title")}
      onCancel={onClose}
      footer={footer}
      width={480}
    >
      <div className="update-dialog">
        {loading && (
          <div className="update-dialog__center">
            <Spin />
            <div className="update-dialog__muted">{t("update.checking")}</div>
          </div>
        )}

        {!loading && failed && (
          <div className="update-dialog__center">
            <CloseCircleOutlined className="update-dialog__icon update-dialog__icon--error" />
            <div className="update-dialog__title">{t("update.checkFailed")}</div>
          </div>
        )}

        {!loading && !failed && info && !updateAvailable && (
          <div className="update-dialog__center">
            <CheckCircleFilled className="update-dialog__icon update-dialog__icon--ok" />
            <div className="update-dialog__title">{t("update.upToDate.title")}</div>
            <div className="update-dialog__muted">
              {t("update.upToDate.detail", { version: info.currentVersion })}
            </div>
          </div>
        )}

        {!loading && !failed && info && updateAvailable && (
          <div className="update-dialog__available">
            <div className="update-dialog__hero">
              <RocketOutlined className="update-dialog__icon update-dialog__icon--accent" />
              <div className="update-dialog__title">{t("update.available.title")}</div>
            </div>

            <div className="update-dialog__versions">
              <div className="update-dialog__version">
                <span className="update-dialog__version-label">{t("update.currentLabel")}</span>
                <span className="update-dialog__version-value">{info.currentVersion}</span>
              </div>
              <ArrowRightOutlined className="update-dialog__arrow" />
              <div className="update-dialog__version update-dialog__version--new">
                <span className="update-dialog__version-label">{t("update.latestLabel")}</span>
                <span className="update-dialog__version-value">{info.latestVersion}</span>
              </div>
            </div>

            {info.hasManifest && info.changedFileCount > 0 && (
              <div className="update-dialog__changeset">
                {t("update.changeSummary", {
                  count: info.changedFileCount,
                  size: formatBytes(info.downloadSize),
                })}
              </div>
            )}

            {info.releaseName && (
              <div className="update-dialog__release-name">{info.releaseName}</div>
            )}

            {info.releaseNotes && (
              <div className="update-dialog__notes-section">
                <div className="update-dialog__notes-label">{t("update.releaseNotes")}</div>
                <div className="update-dialog__notes">{info.releaseNotes}</div>
              </div>
            )}
          </div>
        )}
      </div>
    </FormDialog>
  );
};

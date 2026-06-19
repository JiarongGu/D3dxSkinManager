/**
 * UpdateDialog — app self-update screen (two-phase flow).
 *
 * Phase 1 (this app): check → on "Download", download + stage the update in the background (progress
 * in the Activity panel) → flips to "ready: restart to apply".
 * Phase 2 (C++ launcher): applies the staged update on the next startup (a running exe can't replace
 * itself). See docs/LAUNCHER_ARCHITECTURE.md.
 *
 * Entry points:
 *  - Manual: opened from Settings (no `prefetched`) → runs the check itself.
 *  - Auto (startup): App.tsx checks silently and opens with `prefetched`.
 * On open it also asks the backend whether an update is already staged → shows the "ready" state.
 * Built on the shared FormDialog (no-blink theming baked in).
 */

import React, { useState, useEffect, useCallback, useRef } from "react";
import { Spin } from "antd";
import {
  CheckCircleFilled,
  RocketOutlined,
  DownloadOutlined,
  ArrowRightOutlined,
  CloseCircleOutlined,
  ReloadOutlined,
} from "@ant-design/icons";
import { useTranslation } from "react-i18next";
import { FormDialog } from "../../../shared/components/dialogs/FormDialog";
import { CompactButton, CompactSpace } from "../../../shared/components/compact";
import { systemService, UpdateInfo } from "../../../shared/services/ipc";
import { handleError } from "../../../shared/utils/errorHandler";
import { logger } from "../../../shared/utils/logger";
import { formatBytes } from "../../../shared/utils/formatBytes";
import "./UpdateDialog.css";

type Phase = "checking" | "failed" | "uptodate" | "available" | "downloading" | "ready";

interface UpdateDialogProps {
  open: boolean;
  onClose: () => void;
  /** Pre-fetched result (auto-check on startup). When set, no check is run on open. */
  prefetched?: UpdateInfo;
}

export const UpdateDialog: React.FC<UpdateDialogProps> = ({ open, onClose, prefetched }) => {
  const { t } = useTranslation();
  const [phase, setPhase] = useState<Phase>("checking");
  const [info, setInfo] = useState<UpdateInfo | undefined>(prefetched);
  const [readyVersion, setReadyVersion] = useState<string>("");
  const pollRef = useRef<ReturnType<typeof setInterval> | undefined>(undefined);

  const stopPolling = useCallback(() => {
    if (pollRef.current) {
      clearInterval(pollRef.current);
      pollRef.current = undefined;
    }
  }, []);

  const runCheck = useCallback(async () => {
    setPhase("checking");
    try {
      const result = await systemService.checkForUpdate();
      setInfo(result);
      setPhase(result.updateAvailable ? "available" : "uptodate");
    } catch (error: unknown) {
      logger.error("[UpdateDialog] Update check failed:", error);
      setPhase("failed");
    }
  }, []);

  // On open: if an update is already staged → "ready"; else use prefetched info or run a check.
  useEffect(() => {
    if (!open) return;
    let cancelled = false;
    void (async () => {
      try {
        const state = await systemService.getUpdateState();
        if (cancelled) return;
        if (state.pending) {
          setReadyVersion(state.pendingVersion);
          setPhase("ready");
          return;
        }
      } catch {
        // ignore — fall through to the normal check
      }
      if (cancelled) return;
      if (prefetched) {
        setInfo(prefetched);
        setPhase(prefetched.updateAvailable ? "available" : "uptodate");
      } else {
        void runCheck();
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [open, prefetched, runCheck]);

  // Stop polling when the dialog closes / unmounts.
  useEffect(() => {
    if (!open) stopPolling();
    return stopPolling;
  }, [open, stopPolling]);

  const handleDownload = useCallback(async () => {
    setPhase("downloading");
    try {
      await systemService.downloadUpdate();
      // Poll for completion (progress itself shows in the Activity panel).
      stopPolling();
      pollRef.current = setInterval(async () => {
        try {
          const state = await systemService.getUpdateState();
          if (state.pending) {
            stopPolling();
            setReadyVersion(state.pendingVersion);
            setPhase("ready");
          }
        } catch {
          /* keep polling */
        }
      }, 1500);
    } catch (error: unknown) {
      stopPolling();
      setPhase(info?.updateAvailable ? "available" : "uptodate");
      handleError(error);
    }
  }, [info, stopPolling]);

  // Footer depends on phase.
  let footer: React.ReactNode;
  if (phase === "available") {
    footer = (
      <CompactSpace className="update-dialog__footer">
        <CompactButton onClick={onClose}>{t("update.later")}</CompactButton>
        <CompactButton type="primary" icon={<DownloadOutlined />} onClick={handleDownload}>
          {t("update.download")}
        </CompactButton>
      </CompactSpace>
    );
  } else if (phase === "downloading") {
    footer = (
      <CompactSpace className="update-dialog__footer">
        <CompactButton onClick={onClose}>{t("update.continueInBackground")}</CompactButton>
      </CompactSpace>
    );
  } else {
    footer = (
      <CompactSpace className="update-dialog__footer">
        <CompactButton type="primary" onClick={onClose}>{t("update.close")}</CompactButton>
      </CompactSpace>
    );
  }

  return (
    <FormDialog
      visible={open}
      title={t("update.title")}
      onCancel={onClose}
      footer={footer}
      width={480}
    >
      <div className="update-dialog">
        {phase === "checking" && (
          <div className="update-dialog__center">
            <Spin />
            <div className="update-dialog__muted">{t("update.checking")}</div>
          </div>
        )}

        {phase === "downloading" && (
          <div className="update-dialog__center">
            <Spin />
            <div className="update-dialog__title">{t("update.downloading.title")}</div>
            <div className="update-dialog__muted">{t("update.downloading.detail")}</div>
          </div>
        )}

        {phase === "ready" && (
          <div className="update-dialog__center">
            <CheckCircleFilled className="update-dialog__icon update-dialog__icon--ok" />
            <div className="update-dialog__title">{t("update.ready.title")}</div>
            <div className="update-dialog__muted">
              {t("update.ready.detail", { version: readyVersion || info?.latestVersion || "" })}
            </div>
            <div className="update-dialog__restart-hint">
              <ReloadOutlined /> {t("update.ready.restartHint")}
            </div>
          </div>
        )}

        {phase === "failed" && (
          <div className="update-dialog__center">
            <CloseCircleOutlined className="update-dialog__icon update-dialog__icon--error" />
            <div className="update-dialog__title">{t("update.checkFailed")}</div>
          </div>
        )}

        {phase === "uptodate" && info && (
          <div className="update-dialog__center">
            <CheckCircleFilled className="update-dialog__icon update-dialog__icon--ok" />
            <div className="update-dialog__title">{t("update.upToDate.title")}</div>
            <div className="update-dialog__muted">
              {t("update.upToDate.detail", { version: info.currentVersion })}
            </div>
          </div>
        )}

        {phase === "available" && info && (
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

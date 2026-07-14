import React, { useCallback, useEffect, useRef, useState } from "react";
import { CloudOutlined, LoginOutlined, LogoutOutlined } from "@ant-design/icons";
import { useTranslation } from "react-i18next";
import { CompactCard, CompactButton } from "../../../shared/components/compact";
import { StatusTag } from "../../../shared/components/common/StatusTag";
import { useProfile } from "../../../shared/context/ProfileContext";
import { api } from "../../../shared/services/ipc";
import { Module, SystemEventType } from "../../../shared/services/eventBus";
import { useEventSubscription } from "../../../shared/hooks/useEventSubscription";
import { handleError } from "../../../shared/utils/errorHandler";
import { notification } from "../../../shared/utils/notification";
import type { OnlineStorageAccountInfo } from "../../../shared/types/remote.types";
import "./OnlineStorageAccountsCard.css";

/**
 * Manage logins for download hosts whose files need authentication (Quark). "Log in" opens an
 * in-app browser window on the host's own login page; on close the session cookie is captured and
 * stored — the app never sees a typed password. Downloads from that host then resolve in-app.
 */
const PROVIDERS: { id: string; name: string; hint: string }[] = [
  { id: "quark", name: "夸克网盘 · Quark", hint: "pan.quark.cn" },
  { id: "baidu", name: "百度网盘 · Baidu", hint: "pan.baidu.com" },
];

export const OnlineStorageAccountsCard: React.FC = () => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();
  const [accounts, setAccounts] = useState<OnlineStorageAccountInfo[]>([]);
  const [busy, setBusy] = useState<string>();
  const busyTimerRef = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);

  // Stop the button spinner and cancel its backstop timer.
  const stopBusy = useCallback(() => {
    if (busyTimerRef.current) {
      clearTimeout(busyTimerRef.current);
      busyTimerRef.current = undefined;
    }
    setBusy(undefined);
  }, []);

  const refresh = useCallback(async () => {
    if (!selectedProfileId) return;
    try {
      setAccounts(await api.remote.accountList(selectedProfileId));
    } catch (error: unknown) {
      handleError(error);
    }
  }, [selectedProfileId]);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  // The login window is fire-and-forget (a real QR login outlives the IPC bridge timeout), so the
  // button stays busy from click until the backend says the window has popped up (LOGIN_WINDOW_SHOWN)
  // or a silent cookie refresh finished (ONLINE_ACCOUNT_CHANGED) — not until the login call's ack.
  useEventSubscription(Module.SYSTEM, SystemEventType.LOGIN_WINDOW_SHOWN, () => {
    stopBusy();
  }, [stopBusy]);
  useEventSubscription(Module.SYSTEM, SystemEventType.ONLINE_ACCOUNT_CHANGED, () => {
    stopBusy();
    void refresh();
  }, [refresh, stopBusy]);

  // Cancel any pending backstop timer on unmount.
  useEffect(() => () => {
    if (busyTimerRef.current) clearTimeout(busyTimerRef.current);
  }, []);

  const accountFor = (provider: string) => accounts.find((a) => a.provider === provider);

  const handleLogin = async (provider: string) => {
    if (!selectedProfileId) return;
    try {
      setBusy(provider);
      await api.remote.accountLogin(selectedProfileId, provider); // opens the login window (ack only)
      notification.info(t("onlineStorage.loginWindowOpened"));
      // Keep the button busy — the window takes a moment to init WebView2 + load. It clears when the
      // backend reveals the window (LOGIN_WINDOW_SHOWN) or a silent refresh finishes. Backstop: drop
      // busy after 30s so the button can't spin forever if neither event arrives.
      if (busyTimerRef.current) clearTimeout(busyTimerRef.current);
      busyTimerRef.current = setTimeout(() => setBusy(undefined), 30000);
    } catch (error: unknown) {
      stopBusy();
      handleError(error);
    }
  };

  const handleLogout = async (provider: string) => {
    if (!selectedProfileId) return;
    try {
      setBusy(provider);
      setAccounts(await api.remote.accountRemove(selectedProfileId, provider));
      notification.success(t("onlineStorage.loggedOut"));
    } catch (error: unknown) {
      handleError(error);
    } finally {
      setBusy(undefined);
    }
  };

  return (
    <CompactCard title={<><CloudOutlined /> {t("settings.tabs.onlineStorage")}</>}>
      <p className="online-storage__intro">{t("onlineStorage.intro")}</p>
      <div className="online-storage__list">
        {PROVIDERS.map((p) => {
          const acc = accountFor(p.id);
          const loggedIn = acc?.loggedIn ?? false;
          return (
            <div key={p.id} className="online-storage__row">
              <div className="online-storage__meta">
                <span className="online-storage__name">{p.name}</span>
                <span className="online-storage__host">{p.hint}</span>
              </div>
              <StatusTag
                tone={loggedIn ? "success" : "neutral"}
                label={loggedIn ? t("onlineStorage.statusLoggedIn") : t("onlineStorage.statusLoggedOut")}
              />
              <div className="online-storage__actions">
                <CompactButton
                  size="small"
                  type={loggedIn ? "default" : "primary"}
                  icon={<LoginOutlined />}
                  loading={busy === p.id}
                  onClick={() => void handleLogin(p.id)}
                >
                  {loggedIn ? t("onlineStorage.reLogin") : t("onlineStorage.login")}
                </CompactButton>
                {loggedIn && (
                  <CompactButton
                    size="small"
                    icon={<LogoutOutlined />}
                    disabled={busy === p.id}
                    onClick={() => void handleLogout(p.id)}
                  >
                    {t("onlineStorage.logout")}
                  </CompactButton>
                )}
              </div>
            </div>
          );
        })}
      </div>
    </CompactCard>
  );
};

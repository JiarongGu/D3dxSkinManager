import React, { useCallback, useEffect, useState } from "react";
import { CloudOutlined, LoginOutlined, LogoutOutlined } from "@ant-design/icons";
import { useTranslation } from "react-i18next";
import { CompactCard, CompactButton } from "../../../shared/components/compact";
import { StatusTag } from "../../../shared/components/common/StatusTag";
import { useProfile } from "../../../shared/context/ProfileContext";
import { api } from "../../../shared/services/ipc";
import { eventBus, Module, SystemEventType } from "../../../shared/services/eventBus";
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
];

export const OnlineStorageAccountsCard: React.FC = () => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();
  const [accounts, setAccounts] = useState<OnlineStorageAccountInfo[]>([]);
  const [busy, setBusy] = useState<string>();

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
  // status updates on the backend ONLINE_ACCOUNT_CHANGED event, not the login call's result.
  useEffect(() => {
    const unsub = eventBus.subscribe(Module.SYSTEM, SystemEventType.ONLINE_ACCOUNT_CHANGED, () => {
      setBusy(undefined);
      void refresh();
    });
    return () => unsub();
  }, [refresh]);

  const accountFor = (provider: string) => accounts.find((a) => a.provider === provider);

  const handleLogin = async (provider: string) => {
    if (!selectedProfileId) return;
    try {
      setBusy(provider);
      await api.remote.accountLogin(selectedProfileId, provider); // opens the login window (ack only)
      notification.info(t("onlineStorage.loginWindowOpened"));
    } catch (error: unknown) {
      handleError(error);
    } finally {
      // The IPC just OPENS the window (fire-and-forget); clear busy now so the button is usable again
      // — don't keep it spinning for the window's whole lifetime. The status flips to 已登录 later via
      // the ONLINE_ACCOUNT_CHANGED event.
      setBusy(undefined);
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

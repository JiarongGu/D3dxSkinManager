/**
 * WebView Bridge Service - Communication layer between React and .NET backend
 * Uses WebView2's chrome.webview API for IPC
 */

import { v4 as uuidv4 } from 'uuid';
import {
  MessageType,
  ModuleName,
  BridgeMessage,
  BridgeResponse,
} from "../types/message.types";
import { eventBus } from "./eventBus";

/**
 * WebView2 message event structure
 */
interface WebViewMessageEvent {
  data: string;
}

// WebView2 bridge interface
declare global {
  interface Window {
    chrome?: {
      webview?: {
        postMessage: (message: string) => void;
        addEventListener: (
          event: string,
          handler: (event: WebViewMessageEvent) => void,
        ) => void;
      };
    };
  }
}

// Check if WebView2 is available
function isWebViewAvailable(): boolean {
  return !!window.chrome?.webview?.postMessage;
}

/**
 * DEV-only canned IPC responses for "pure-UI" mode: running the React app in a plain Chrome tab
 * (Vite dev server) where there is NO WebView2 bridge. Returns just enough bootstrap data for the
 * app shell (settings + a fake profile + empty lists) to render so components/layouts can be verified
 * in the browser without the desktop backend. See .claude/knowledge/desktop-app-testing.md.
 * Never used when WebView2 is present, and stripped from production builds.
 */
function getDevMockResponse(module: string, type: string): unknown {
  const key = `${module}:${type}`;
  const mocks: Record<string, unknown> = {
    'SETTING:GET_GLOBAL': { theme: 'dark', annotationLevel: 'standard', logLevel: 'info', language: 'en', autoUpdateCheck: false, window: {} },
    'PROFILE:GET_ALL': { profiles: [{ id: 'dev', profileId: 'dev', name: 'Dev Profile', description: 'pure-UI preview' }], activeProfileId: 'dev' },
    'PROFILE:GET_ACTIVE': { id: 'dev', profileId: 'dev', name: 'Dev Profile' },
    'PROFILE:GET_CONFIG': {},
    'MOD:GET_STATISTICS': { totalMods: 0, loadedMods: 0, availableMods: 0, totalCategories: 0, totalAuthors: 0 },
    'MOD:GET_UNCLASSIFIED_COUNT': 0,
  };
  if (key in mocks) return mocks[key];
  // Permissive default: list-style reads expect an array, everything else an object.
  return /GET_ALL|GET_TREE|GET_ACTIVE_MODS|GET_LOADED|GET_TAGS|GET_AUTHORS|GET_PRESETS|LIST|SEARCH/.test(type) ? [] : {};
}

// Generate unique ID for this WebView instance (for multi-window support in future)
// Created at module level so it persists across hot-reloads during development
const webViewId = uuidv4();

class BridgeService {
  private messageHandlers: Map<string, (response: BridgeResponse) => void> =
    new Map();
  // Global modules that don't require profileId
  private readonly globalModules = ["APP", "SETTING", "PROFILE", "SYSTEM"];
  // Expose webViewId as readonly property
  public readonly webViewId = webViewId;

  constructor() {
    this.initializeMessageReceiver();
  }

  /**
   * Initialize message receiver
   * All messages have a category field that determines routing:
   * - category: "IPC" -> IPC request/response
   * - category: "NOTIFICATION" -> Push events/notifications -> emit to eventBus
   */
  private initializeMessageReceiver() {
    if (!window.chrome?.webview?.addEventListener) {
            return;
    }

    window.chrome.webview.addEventListener("message", (event: WebViewMessageEvent) => {
      try {
        const parsed = JSON.parse(event.data) as { category?: string; [key: string]: unknown };

        // Route based on category
        if (parsed.category === "IPC") {
          // IPC request/response - validate it has required fields
          if ('id' in parsed && 'success' in parsed) {
            const response = parsed as unknown as BridgeResponse;
            const handler = this.messageHandlers.get(response.id);

            if (handler) {
              handler(response);
              this.messageHandlers.delete(response.id);
            }
          }
        } else if (parsed.category === "NOTIFICATION") {
          // Push notification/event - emit to eventBus
          // Backend sends: { category, id, module, type, payload, timestamp }
          if ('module' in parsed && 'type' in parsed && typeof parsed.module === 'string' && typeof parsed.type === 'string') {
            const { module, type, payload } = parsed as { module: string; type: string; payload: unknown };

            // Handle batched events (unbundle them)
            if (module === "EVENT_BUS" && type === "BATCH" && Array.isArray(payload)) {
              // Unbundle batch - emit each event individually
              payload.forEach((event: unknown) => {
                if (event && typeof event === 'object' && 'module' in event && 'type' in event) {
                  // Cast module string to Module enum type (runtime values match)
                  eventBus.emit({
                    module: event.module as unknown as import('./eventBus').Module,
                    type: event.type as string,
                    payload: 'payload' in event ? event.payload : undefined,
                  });
                }
              });
            } else {
              // Single event - emit directly. Cast module string to Module enum type
              eventBus.emit({
                module: module as unknown as import('./eventBus').Module,
                type,
                payload,
              });
            }
          }
        }
      } catch (error) {
        console.error('[bridgeService] Failed to handle incoming message:', error);
      }
    });
  }

  /**
   * Notify backend that WebView is ready and app is fully initialized
   * This clears stale drop zones and hides the splash screen
   */
  async notifyWebViewReady(): Promise<void> {
    try {
      await this.sendMessage({
        module: 'APP',
        type: 'WEBVIEW_READY',
        payload: { webViewId: this.webViewId }
      });
    } catch (error) {
      console.error('Failed to notify WebView ready:', error);
    }
  }

  /**
   * Send a message to the .NET backend and wait for response
   * @param module - The module to route the message to (e.g., 'MOD', 'PROFILE')
   * @param type - The action type within the module (e.g., 'GET_ALL', 'LOAD')
   * @param profileId - Optional profile ID for modules that require it
   * @param payload - Optional payload data
   */
  sendMessage<T, TPayload = unknown>({
    module,
    type,
    profileId,
    payload,
  }: {
    module: ModuleName;
    type: MessageType;
    profileId?: string;
    payload?: TPayload;
  }): Promise<T> {
    return new Promise((resolve, reject) => {
      const id = uuidv4();

      // Determine if this module requires profileId
      const needsProfileId = !this.globalModules.includes(module);

      // Fail fast with a diagnostic when a profile-scoped module is called without a profileId. The
      // message would otherwise be sent and rejected by the backend with an opaque error — the guard
      // body was empty, so misuse was invisible at the call site.
      if (needsProfileId && !profileId) {
        const err = new Error(
          `[bridgeService] Missing profileId for profile-scoped call ${module}.${type}`,
        );
        console.error(err.message);
        reject(err);
        return;
      }

      const message: BridgeMessage = {
        id,
        module,
        type,
        profileId,
        payload,
      };

      // Register response handler
      this.messageHandlers.set(id, (response: BridgeResponse) => {
        if (response.success) {
          resolve(response.data as T);
        } else {
          const error = new Error(
            response.error || "Unknown error",
          ) as Error & { errorDetails?: typeof response.errorDetails };
          // Attach errorDetails to the error object for error handling middleware
          if (response.errorDetails) {
            error.errorDetails = response.errorDetails;
          }
          reject(error);
        }
      });

      // Send message to .NET
      if (!isWebViewAvailable()) {
        // DEV pure-UI mode (plain Chrome, no WebView2): resolve with canned data so the React shell
        // renders for component/design verification instead of hard-failing. Prod still errors.
        if (import.meta.env.DEV) {
          this.messageHandlers.delete(id);
          // Serve REAL translations so the UI isn't full of raw i18n keys in pure-UI mode: load the
          // sibling backend Languages/*.json on demand (allowed via vite server.fs.allow).
          if (module === 'SETTING' && type === 'GET_LANGUAGE') {
            const code = (payload as { languageCode?: string } | undefined)?.languageCode === 'cn' ? 'cn' : 'en';
            const load = code === 'cn'
              ? import('../../../../D3dxSkinManager/Languages/cn.json')
              : import('../../../../D3dxSkinManager/Languages/en.json');
            // The JSON file IS a LanguageSettings ({ code, name, translations }) — return it as-is.
            load
              .then((m) => resolve({ success: true, language: (m as { default: unknown }).default } as T))
              .catch(() => resolve(getDevMockResponse(module, type) as T));
            return;
          }
          resolve(getDevMockResponse(module, type) as T);
          return;
        }
        const error = new Error("WebView2 not available - application must run in desktop mode");
        this.messageHandlers.delete(id);
        reject(error);
        return;
      }

      window.chrome!.webview!.postMessage(JSON.stringify(message));

      // Timeout after 30 seconds
      setTimeout(() => {
        if (this.messageHandlers.has(id)) {
          this.messageHandlers.delete(id);
          reject(new Error("Request timeout"));
        }
      }, 30000);
    });
  }
}

// Export singleton instance
export const bridgeService = new BridgeService();

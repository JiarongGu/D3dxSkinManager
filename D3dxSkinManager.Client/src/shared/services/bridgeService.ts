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

// WebView2 bridge interface
declare global {
  interface Window {
    chrome?: {
      webview?: {
        postMessage: (message: string) => void;
        addEventListener: (
          event: string,
          handler: (event: any) => void,
        ) => void;
      };
    };
  }
}

// Check if WebView2 is available
function isWebViewAvailable(): boolean {
  return !!window.chrome?.webview?.postMessage;
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

    window.chrome.webview.addEventListener("message", (event: any) => {
      try {
        const parsed = JSON.parse(event.data);

        // Route based on category
        if (parsed.category === "IPC") {
          // IPC request/response
          const response: BridgeResponse = parsed;
          const handler = this.messageHandlers.get(response.id);

          if (handler) {
            handler(response);
            this.messageHandlers.delete(response.id);
          }
        } else if (parsed.category === "NOTIFICATION") {
          // Push notification/event - emit to eventBus
          // Backend sends: { category, id, module, type, payload, timestamp }
          const { module, type, payload } = parsed;

          // Handle batched events (unbundle them)
          if (module === "EVENT_BUS" && type === "BATCH" && Array.isArray(payload)) {
            // Unbundle batch - emit each event individually
            payload.forEach((event: any) => {
              eventBus.emit({
                module: event.module,
                type: event.type,
                payload: event.payload,
              });
            });
          } else {
            // Single event - emit directly
            eventBus.emit({
              module,
              type,
              payload,
            });
          }
        }
      } catch (error) {
              }
    });
  }

  /**
   * Notify backend that WebView is ready
   * This should be called once during app initialization to clear any stale drop zones
   */
  async notifyWebViewReady(): Promise<void> {
    try {
      await this.sendMessage({
        module: 'APP',
        type: 'WEBVIEW_READY',
        payload: { webViewId: this.webViewId }
      });
          } catch (error) {
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

      // Check if profileId is required but missing
      if (needsProfileId && !profileId) {
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
        const error = new Error("WebView2 not available - application must run in desktop mode");
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

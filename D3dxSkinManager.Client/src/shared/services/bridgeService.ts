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
import { eventBus, EventType } from "./eventBus";

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

class BridgeService {
  private messageHandlers: Map<string, (response: BridgeResponse) => void> =
    new Map();
  // Global modules that don't require profileId
  private readonly globalModules = ["SETTINGS", "PROFILE", "SYSTEM"];

  constructor() {
    this.initializeMessageReceiver();
  }

  /**
   * Initialize message receiver
   * All messages have a category field that determines routing:
   * - category: "ipc" -> IPC request/response
   * - category: "notification" -> Push events/notifications -> emit to eventBus
   */
  private initializeMessageReceiver() {
    if (!window.chrome?.webview?.addEventListener) {
      console.warn(
        "[BridgeService] WebView2 not available - running in development mode",
      );
      return;
    }

    window.chrome.webview.addEventListener("message", (event: any) => {
      try {
        const parsed = JSON.parse(event.data);

        // Route based on category
        if (parsed.category === "ipc") {
          // IPC request/response
          const response: BridgeResponse = parsed;
          const handler = this.messageHandlers.get(response.id);

          if (handler) {
            handler(response);
            this.messageHandlers.delete(response.id);
          }
        } else if (parsed.category === "notification") {
          // Push notification/event - emit to eventBus
          // Frontend subscribers use the 'type' to identify which event they want
          eventBus.emit({
            type: parsed.type as EventType,
            eventName: parsed.eventName,
            data: parsed.data,
          });
        }
      } catch (error) {
        console.error("[BridgeService] Failed to parse message:", error);
      }
    });
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
      const needsProfileId = !this.globalModules.includes(module.toUpperCase());

      // Check if profileId is required but missing
      if (needsProfileId && !profileId) {
        console.warn(
          `No profile selected for module ${module}, request may fail`,
        );
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

/**
 * WebView Bridge Service - Communication layer between React and .NET backend
 * Uses WebView2's chrome.webview API for IPC
 */

import {
  MessageType,
  ModuleName,
  BridgeMessage,
  BridgeResponse,
} from "../types/message.types";
import { OperationNotificationMessage } from "../types/operation.types";

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
  private operationNotificationHandlers: Array<
    (notification: OperationNotificationMessage["notification"]) => void
  > = [];
  private filesDroppedHandlers: Array<(filePaths: string[]) => void> = [];
  private messageId = 0;
  // Global modules that don't require profileId
  private readonly globalModules = ["SETTINGS", "PROFILE", "SYSTEM"];

  constructor() {
    this.initializeMessageReceiver();
  }

  /**
   * Subscribe to operation notifications from backend
   * Returns unsubscribe function
   */
  subscribeToOperationNotifications(
    handler: (
      notification: OperationNotificationMessage["notification"],
    ) => void,
  ): () => void {
    this.operationNotificationHandlers.push(handler);
    return () => {
      const index = this.operationNotificationHandlers.indexOf(handler);
      if (index > -1) {
        this.operationNotificationHandlers.splice(index, 1);
      }
    };
  }

  /**
   * Subscribe to file drop events from OS-level drag-drop
   * Returns unsubscribe function
   */
  subscribeToFilesDropped(handler: (filePaths: string[]) => void): () => void {
    this.filesDroppedHandlers.push(handler);
    return () => {
      const index = this.filesDroppedHandlers.indexOf(handler);
      if (index > -1) {
        this.filesDroppedHandlers.splice(index, 1);
      }
    };
  }

  private initializeMessageReceiver() {
    // Listen for messages from .NET backend
    if (window.chrome?.webview?.addEventListener) {
      window.chrome.webview.addEventListener("message", (event: any) => {
        try {
          const parsed = JSON.parse(event.data);

          // Check if this is an operation notification (push message)
          if (parsed.type === "OPERATION_NOTIFICATION") {
            const operationNotification =
              parsed as OperationNotificationMessage;
            // Notify all subscribers
            this.operationNotificationHandlers.forEach((handler) => {
              try {
                handler(operationNotification.notification);
              } catch (error) {
                console.error(
                  "Error in operation notification handler:",
                  error,
                );
              }
            });
            return;
          }

          // Check if this is a FILES_DROPPED push message
          if (parsed.type === "FILES_DROPPED") {
            const filePaths = parsed.filePaths as string[];
            console.log("[BridgeService] FILES_DROPPED received:", filePaths);
            // Notify all subscribers
            this.filesDroppedHandlers.forEach((handler) => {
              try {
                handler(filePaths);
              } catch (error) {
                console.error("Error in files dropped handler:", error);
              }
            });
            return;
          }

          // Otherwise, it's a regular response message
          const response: BridgeResponse = parsed;
          const handler = this.messageHandlers.get(response.id);

          if (handler) {
            handler(response);
            this.messageHandlers.delete(response.id);
          }
        } catch (error) {
          console.error("Failed to parse message from backend:", error);
        }
      });
    } else {
      console.warn(
        "[BridgeService] WebView2 not available - running in development mode",
      );
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
      const id = `msg_${++this.messageId}_${Date.now()}`;

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

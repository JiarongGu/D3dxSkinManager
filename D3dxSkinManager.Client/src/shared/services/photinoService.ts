/**
 * Photino Bridge - Communication layer between React and .NET backend
 */

import { MessageType, ModuleName, PhotinoMessage, PhotinoResponse } from '../types/message.types';

// Photino window interface
interface PhotinoWindow {
  sendMessage: (message: string) => void;
  receiveMessage: (callback: (message: string) => void) => void;
}

declare global {
  interface Window {
    chrome?: {
      webview?: PhotinoWindow;
    };
  }
}

// Helper to get Photino bridge (works with both old and new Photino versions)
function getPhotinoBridge(): PhotinoWindow | undefined {
  // @ts-ignore - Check for external without type errors
  if (window.external && typeof window.external.sendMessage === 'function') {
    // @ts-ignore
    return window.external;
  }
  // New Photino uses chrome.webview
  if (window.chrome?.webview) {
    return window.chrome.webview;
  }
  return undefined;
}

class PhotinoService {
  private messageHandlers: Map<string, (response: PhotinoResponse) => void> = new Map();
  private messageId = 0;
  // Global modules that don't require profileId
  private readonly globalModules = ['SETTINGS', 'PROFILE', 'SYSTEM'];

  constructor() {
    this.initializeMessageReceiver();
  }

  private initializeMessageReceiver() {
    // Listen for messages from .NET backend
    const bridge = getPhotinoBridge();
    if (bridge?.receiveMessage) {
      bridge.receiveMessage((message: string) => {
        try {
          const response: PhotinoResponse = JSON.parse(message);
          const handler = this.messageHandlers.get(response.id);

          if (handler) {
            handler(response);
            this.messageHandlers.delete(response.id);
          }
        } catch (error) {
          console.error('Failed to parse message from backend:', error);
        }
      });
    }
  }

  /**
   * Send a message to the .NET backend and wait for response
   * @param module - The module to route the message to (e.g., 'MOD', 'PROFILE')
   * @param type - The action type within the module (e.g., 'GET_ALL', 'LOAD')
   * @param profileId - Optional profile ID for modules that require it
   * @param payload - Optional payload data
   */
  sendMessage<T, TPayload = unknown>({module, type, profileId, payload}: {module: ModuleName; type: MessageType; profileId?: string; payload?: TPayload}): Promise<T> {
    return new Promise((resolve, reject) => {
      const id = `msg_${++this.messageId}_${Date.now()}`;

      // Determine if this module requires profileId
      const needsProfileId = !this.globalModules.includes(module.toUpperCase());

      // Check if profileId is required but missing
      if (needsProfileId && !profileId) {
        console.warn(`No profile selected for module ${module}, request may fail`);
      }

      const message: PhotinoMessage = {
        id,
        module,
        type,
        profileId,
        payload
      };

      // Register response handler
      this.messageHandlers.set(id, (response: PhotinoResponse) => {
        if (response.success) {
          resolve(response.data as T);
        } else {
          reject(new Error(response.error || 'Unknown error'));
        }
      });

      // Send message to .NET
      const bridge = getPhotinoBridge();
      if (bridge?.sendMessage) {
        bridge.sendMessage(JSON.stringify(message));
      } else {
        // Development fallback - simulate backend
        console.warn('Running without Photino backend (development mode)');
        this.simulateBackendResponse(message);
      }

      // Timeout after 30 seconds
      setTimeout(() => {
        if (this.messageHandlers.has(id)) {
          this.messageHandlers.delete(id);
          reject(new Error('Request timeout'));
        }
      }, 30000);
    });
  }

  /**
   * Development mode: Simulate backend responses
   */
  private simulateBackendResponse(message: PhotinoMessage) {
    setTimeout(() => {
      const handler = this.messageHandlers.get(message.id);
      if (!handler) return;

      let response: PhotinoResponse = {
        id: message.id,
        success: true,
        data: null
      };

      // Mock responses based on module and type
      if (message.module === 'MOD') {
        switch (message.type) {
          case 'GET_ALL':
            response.data = this.getMockMods();
            break;
          case 'GET_LOADED':
            response.data = [];
            break;
          case 'LOAD':
          case 'UNLOAD':
            response.data = true;
            break;
          default:
            response.success = false;
            response.error = 'Not implemented in dev mode';
        }
      } else if (message.module === 'PROFILE') {
        switch (message.type) {
          case 'GET_ALL':
            response.data = this.getMockProfileList();
            break;
          case 'GET_ACTIVE':
            response.data = this.getMockActiveProfile();
            break;
          case 'GET_BY_ID':
            response.data = this.getMockActiveProfile();
            break;
          case 'CREATE':
            const createPayload = message.payload as { name?: string; description?: string } | undefined;
            response.data = {
              id: 'profile_' + Date.now(),
              name: createPayload?.name || 'New Profile',
              description: createPayload?.description || '',
              isActive: false,
              createdAt: new Date().toISOString(),
              lastUsedAt: new Date().toISOString(),
              modCount: 0,
              totalSize: 0
            };
            break;
          case 'SWITCH':
            response.data = {
              success: true,
              activeProfile: this.getMockActiveProfile(),
              message: 'Switched to profile'
            };
            break;
          default:
            response.data = true;
        }
      } else {
        response.success = false;
        response.error = `Module ${message.module} not mocked in dev mode`;
      }

      handler(response);
    }, 300); // Simulate network delay
  }

  private getMockMods() {
    return [
      {
        sha: 'abc123',
        category: 'Nahida',
        name: 'Nahida Summer Outfit',
        author: 'TestAuthor',
        description: 'A beautiful summer outfit for Nahida',
        type: '7z',
        grading: 'G',
        tags: ['Nahida', 'Summer', 'Outfit'],
        isLoaded: false,
        isAvailable: true
      },
      {
        sha: 'def456',
        category: 'Nahida',
        name: 'Nahida Winter Coat',
        author: 'TestAuthor2',
        description: 'Warm winter coat',
        type: '7z',
        grading: 'G',
        tags: ['Nahida', 'Winter'],
        isLoaded: false,
        isAvailable: true
      }
    ];
  }

  private getMockActiveProfile() {
    return {
      id: 'default',
      name: 'Default',
      description: 'Default profile',
      isActive: true,
      createdAt: new Date().toISOString(),
      lastUsedAt: new Date().toISOString(),
      modCount: 2,
      totalSize: 1024000
    };
  }

  private getMockProfileList() {
    return {
      profiles: [
        {
          id: 'default',
          name: 'Default',
          description: 'Default profile',
          isActive: true,
          createdAt: new Date().toISOString(),
          lastUsedAt: new Date().toISOString(),
          modCount: 2,
          totalSize: 1024000
        },
        {
          id: 'gaming',
          name: 'Gaming',
          description: 'Gaming profile',
          isActive: false,
          createdAt: new Date().toISOString(),
          lastUsedAt: new Date().toISOString(),
          modCount: 5,
          totalSize: 2048000
        }
      ],
      activeProfileId: 'default'
    };
  }
}

// Export singleton instance
export const photinoService = new PhotinoService();

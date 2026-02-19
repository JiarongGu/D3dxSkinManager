/**
 * Keyboard Shortcut Manager (Phase 13)
 * Global keyboard shortcut system with context-aware shortcuts
 */

export interface ShortcutConfig {
  key: string;
  ctrlKey?: boolean;
  shiftKey?: boolean;
  altKey?: boolean;
  metaKey?: boolean;
  description: string;
  callback: () => void;
  preventDefault?: boolean;
  context?: string; // Optional context for context-aware shortcuts
}

export class KeyboardShortcutManager {
  private shortcuts: Map<string, ShortcutConfig> = new Map();
  private activeContext: string | null = null;

  constructor() {
    this.handleKeyDown = this.handleKeyDown.bind(this);
  }

  /**
   * Register a keyboard shortcut
   */
  register(id: string, config: ShortcutConfig): void {
    this.shortcuts.set(id, config);
  }

  /**
   * Unregister a keyboard shortcut
   */
  unregister(id: string): void {
    this.shortcuts.delete(id);
  }

  /**
   * Set the active context for context-aware shortcuts
   */
  setContext(context: string | null): void {
    this.activeContext = context;
  }

  /**
   * Start listening for keyboard events
   */
  start(): void {
    document.addEventListener('keydown', this.handleKeyDown);
  }

  /**
   * Stop listening for keyboard events
   */
  stop(): void {
    document.removeEventListener('keydown', this.handleKeyDown);
  }

  /**
   * Handle keydown events
   */
  private handleKeyDown(event: KeyboardEvent): void {
    // Skip if user is typing in an input field
    const target = event.target as HTMLElement;
    const tagName = target.tagName.toLowerCase();
    const isEditable = target.isContentEditable ||
                       tagName === 'input' ||
                       tagName === 'textarea' ||
                       tagName === 'select';

    // Allow Ctrl+F even in input fields
    if (isEditable && !(event.ctrlKey && event.key.toLowerCase() === 'f')) {
      return;
    }

    const shortcutsArray = Array.from(this.shortcuts.entries());
    for (const [id, config] of shortcutsArray) {
      // Check if shortcut matches current context (if context is specified)
      if (config.context && config.context !== this.activeContext) {
        continue;
      }

      // Check if key matches
      const keyMatches = event.key.toLowerCase() === config.key.toLowerCase();
      const ctrlMatches = config.ctrlKey ? event.ctrlKey : !event.ctrlKey;
      const shiftMatches = config.shiftKey ? event.shiftKey : !event.shiftKey;
      const altMatches = config.altKey ? event.altKey : !event.altKey;
      const metaMatches = config.metaKey ? event.metaKey : !event.metaKey;

      if (keyMatches && ctrlMatches && shiftMatches && altMatches && metaMatches) {
        if (config.preventDefault !== false) {
          event.preventDefault();
          event.stopPropagation();
        }
        config.callback();
        break;
      }
    }
  }

  /**
   * Get all registered shortcuts
   */
  getShortcuts(): Array<{ id: string; config: ShortcutConfig }> {
    return Array.from(this.shortcuts.entries()).map(([id, config]) => ({ id, config }));
  }

  /**
   * Get shortcuts for a specific context
   */
  getShortcutsForContext(context: string | null): Array<{ id: string; config: ShortcutConfig }> {
    return this.getShortcuts().filter(({ config }) =>
      config.context === context || !config.context
    );
  }

  /**
   * Format shortcut key combination for display
   */
  static formatShortcut(config: ShortcutConfig): string {
    const parts: string[] = [];
    if (config.ctrlKey) parts.push('Ctrl');
    if (config.shiftKey) parts.push('Shift');
    if (config.altKey) parts.push('Alt');
    if (config.metaKey) parts.push('Meta');
    parts.push(config.key.toUpperCase());
    return parts.join(' + ');
  }
}

// Create singleton instance
export const keyboardManager = new KeyboardShortcutManager();

/**
 * React hook for using keyboard shortcuts
 */
export const useKeyboardShortcut = (
  id: string,
  config: ShortcutConfig,
  deps: any[] = []
): void => {
  // This will be implemented in the component that uses it
  // using useEffect
};

/**
 * Predefined shortcut configurations
 */
export const SHORTCUTS = {
  // Global shortcuts
  FOCUS_SEARCH: {
    key: 'f',
    ctrlKey: true,
    description: 'Focus search field',
  },
  SAVE: {
    key: 's',
    ctrlKey: true,
    description: 'Save current form',
  },
  CANCEL: {
    key: 'Escape',
    description: 'Cancel/Close dialog',
  },
  SUBMIT: {
    key: 'Enter',
    ctrlKey: true,
    description: 'Submit form',
  },

  // Mod management shortcuts
  SELECT_ALL: {
    key: 'a',
    ctrlKey: true,
    description: 'Select all items',
    context: 'mod-list',
  },
  DELETE: {
    key: 'Delete',
    description: 'Delete selected item',
    context: 'mod-list',
  },
  REFRESH: {
    key: 'F5',
    description: 'Refresh list',
  },

  // Navigation shortcuts
  NEXT_TAB: {
    key: 'Tab',
    ctrlKey: true,
    description: 'Switch to next tab',
  },
  PREV_TAB: {
    key: 'Tab',
    ctrlKey: true,
    shiftKey: true,
    description: 'Switch to previous tab',
  },

  // Import window shortcuts
  CONFIRM_IMPORT: {
    key: 'Enter',
    ctrlKey: true,
    description: 'Confirm import',
    context: 'import-window',
  },
  CANCEL_IMPORT: {
    key: 'Escape',
    description: 'Cancel import',
    context: 'import-window',
  },
};

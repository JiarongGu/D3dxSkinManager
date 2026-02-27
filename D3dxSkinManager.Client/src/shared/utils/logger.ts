/**
 * Log Levels (matching backend C# implementation)
 * Lower number = more verbose
 */
export enum LogLevel {
  VERBOSE = 0,  // Extremely detailed (high-frequency events)
  DEBUG = 1,    // Debug
  INFO = 2,     // Info
  WARN = 3,     // Warnings
  ERROR = 4,    // Errors
  ALL = -1,     // Show everything
  OFF = -2,     // Disable logging
}

export type LogLevelName = 'ALL' | 'VERBOSE' | 'DEBUG' | 'INFO' | 'WARN' | 'ERROR' | 'OFF';

/**
 * Logger class with level-based filtering
 * Log level is now stored in backend global settings
 */
export class Logger {
  // Default to INFO in development, WARN in production
  private currentLevel: LogLevel = process.env.NODE_ENV === 'development' ? LogLevel.INFO : LogLevel.WARN;
  private isInitialized = false;
  private persistToBackend = false; // Whether to send logs to backend for persistence

  constructor() {
    // Load log level from backend
    this.loadLevel();
  }

  /**
   * Enable/disable sending logs to backend for persistence
   */
  setPersistence(enabled: boolean): void {
    this.persistToBackend = enabled;
  }

  /**
   * Get persistence status
   */
  getPersistence(): boolean {
    return this.persistToBackend;
  }

  /**
   * Set current log level and save to backend
   */
  setLevel(level: LogLevel | LogLevelName): void {
    if (typeof level === 'string') {
      this.currentLevel = LogLevel[level];
    } else {
      this.currentLevel = level;
    }

    // Save to backend asynchronously (fire and forget)
    this.saveLevel();
  }

  /**
   * Get current log level
   */
  getLevel(): LogLevel {
    return this.currentLevel;
  }

  /**
   * Get log level name
   */
  getLevelName(level: LogLevel): LogLevelName {
    return LogLevel[level] as LogLevelName;
  }

  /**
   * Get current log level name
   */
  getCurrentLevelName(): LogLevelName {
    return this.getLevelName(this.currentLevel);
  }

  /**
   * Load log level from backend
   */
  private async loadLevel(): Promise<void> {
    try {
      const { settingsService } = await import('../../modules/setting/services/settingsService');
      const settings = await settingsService.getGlobalSettings();
      const level = settings.logLevel as LogLevelName;
      if (level && level in LogLevel) {
        this.currentLevel = LogLevel[level];
      }
      this.isInitialized = true;
    } catch (error) {
      // Silently default to INFO if backend not available
      // This is a dev/debug setting, not critical
      this.currentLevel = LogLevel.INFO;
      this.isInitialized = true;
    }
  }

  /**
   * Save log level to backend
   */
  private async saveLevel(): Promise<void> {
    try {
      const { settingsService } = await import('../../modules/setting/services/settingsService');
      await settingsService.updateGlobalSetting('logLevel', this.getLevelName(this.currentLevel));
    } catch (error) {
      // Silently fail - this is a dev/debug setting
      // Not critical if it doesn't persist
    }
  }

  /**
   * Check if message should be logged
   */
  private shouldLog(level: LogLevel): boolean {
    // OFF disables all logging
    if (this.currentLevel === LogLevel.OFF) {
      return false;
    }

    // ALL enables all logging
    if (this.currentLevel === LogLevel.ALL) {
      return true;
    }

    // Otherwise, log if level >= currentLevel (higher or equal importance)
    return level >= this.currentLevel;
  }

  /**
   * Send log to backend for persistence
   */
  private async sendToBackend(level: LogLevel, message: string, args: any[]): Promise<void> {
    if (!this.persistToBackend) {
      return;
    }

    try {
      const { bridgeService } = await import('../services/bridgeService');
      const levelName = this.getLevelName(level);
      const formattedMessage = args.length > 0
        ? `${message} ${args.map(a => typeof a === 'object' ? JSON.stringify(a) : String(a)).join(' ')}`
        : message;

      await bridgeService.sendMessage({
        module: 'SYSTEM',
        type: 'LOG_FROM_FRONTEND',
        payload: {
          level: levelName,
          message: formattedMessage,
          timestamp: new Date().toISOString(),
          source: 'Frontend'
        }
      });
    } catch (error) {
      // Silently fail - don't create infinite loop if logging itself fails
      // Just log to console as fallback
    }
  }

  /**
   * Log a verbose message (high-frequency events)
   */
  verbose(message: string, ...args: any[]): void {
    if (this.shouldLog(LogLevel.VERBOSE)) {
      console.log(`[VERBOSE] ${message}`, ...args);
      this.sendToBackend(LogLevel.VERBOSE, message, args); // Fire and forget
    }
  }

  /**
   * Log a debug message
   */
  debug(message: string, ...args: any[]): void {
    if (this.shouldLog(LogLevel.DEBUG)) {
      console.log(`[DEBUG] ${message}`, ...args);
      this.sendToBackend(LogLevel.DEBUG, message, args); // Fire and forget
    }
  }

  /**
   * Log an info message
   */
  info(message: string, ...args: any[]): void {
    if (this.shouldLog(LogLevel.INFO)) {
      console.info(`[INFO] ${message}`, ...args);
      this.sendToBackend(LogLevel.INFO, message, args); // Fire and forget
    }
  }

  /**
   * Log a warning message
   */
  warn(message: string, ...args: any[]): void {
    if (this.shouldLog(LogLevel.WARN)) {
      console.warn(`[WARN] ${message}`, ...args);
      this.sendToBackend(LogLevel.WARN, message, args); // Fire and forget
    }
  }

  /**
   * Log an error message
   */
  error(message: string, ...args: any[]): void {
    if (this.shouldLog(LogLevel.ERROR)) {
      console.error(`[ERROR] ${message}`, ...args);
      this.sendToBackend(LogLevel.ERROR, message, args); // Fire and forget
    }
  }

  /**
   * Get all available log level options
   */
  static getLevelOptions(): Array<{ value: string; label: string; description: string }> {
    return [
      { value: 'all', label: 'All', description: 'Show all log messages' },
      { value: 'verbose', label: 'Verbose', description: 'Extremely detailed (high-frequency events)' },
      { value: 'debug', label: 'Debug', description: 'Debug information and above' },
      { value: 'info', label: 'Info', description: 'Information, warnings and errors' },
      { value: 'warn', label: 'Warn', description: 'Warnings and errors' },
      { value: 'error', label: 'Error', description: 'Errors only' },
      { value: 'off', label: 'Off', description: 'Disable all logging' },
    ];
  }
}

// Export singleton instance
export const logger = new Logger();

// Export default for convenient usage
export default logger;

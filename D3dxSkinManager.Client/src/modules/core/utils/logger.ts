/**
 * Log Levels (matching Python implementation)
 * Lower number = more verbose
 */
export enum LogLevel {
  ALL = 0,      // Show everything
  TRACE = 1,    // Detailed traces
  DEBUG = 2,    // Debug information
  INFO = 3,     // General information
  WARN = 4,     // Warnings
  ERROR = 5,    // Errors
  FATAL = 6,    // Fatal errors
  OFF = 7,      // Disable logging
}

export type LogLevelName = 'ALL' | 'TRACE' | 'DEBUG' | 'INFO' | 'WARN' | 'ERROR' | 'FATAL' | 'OFF';

/**
 * Logger class with level-based filtering
 * Log level is now stored in backend global settings
 */
export class Logger {
  // Default to DEBUG in development, INFO in production
  private currentLevel: LogLevel = process.env.NODE_ENV === 'development' ? LogLevel.DEBUG : LogLevel.INFO;
  private isInitialized = false;

  constructor() {
    // Load log level from backend
    this.loadLevel();
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
      const { settingsService } = await import('../../../modules/settings/services/settingsService');
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
      const { settingsService } = await import('../../../modules/settings/services/settingsService');
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
    return level >= this.currentLevel;
  }

  /**
   * Log a trace message
   */
  trace(message: string, ...args: any[]): void {
    if (this.shouldLog(LogLevel.TRACE)) {
      console.log(`[TRACE] ${message}`, ...args);
    }
  }

  /**
   * Log a debug message
   */
  debug(message: string, ...args: any[]): void {
    if (this.shouldLog(LogLevel.DEBUG)) {
      console.log(`[DEBUG] ${message}`, ...args);
    }
  }

  /**
   * Log an info message
   */
  info(message: string, ...args: any[]): void {
    if (this.shouldLog(LogLevel.INFO)) {
      console.info(`[INFO] ${message}`, ...args);
    }
  }

  /**
   * Log a warning message
   */
  warn(message: string, ...args: any[]): void {
    if (this.shouldLog(LogLevel.WARN)) {
      console.warn(`[WARN] ${message}`, ...args);
    }
  }

  /**
   * Log an error message
   */
  error(message: string, ...args: any[]): void {
    if (this.shouldLog(LogLevel.ERROR)) {
      console.error(`[ERROR] ${message}`, ...args);
    }
  }

  /**
   * Log a fatal error message
   */
  fatal(message: string, ...args: any[]): void {
    if (this.shouldLog(LogLevel.FATAL)) {
      console.error(`[FATAL] ${message}`, ...args);
    }
  }

  /**
   * Get all available log level options
   */
  static getLevelOptions(): Array<{ value: LogLevelName; label: string; description: string }> {
    return [
      { value: 'ALL', label: 'ALL', description: 'Show all log messages' },
      { value: 'TRACE', label: 'TRACE', description: 'Show trace and above' },
      { value: 'DEBUG', label: 'DEBUG', description: 'Show debug and above' },
      { value: 'INFO', label: 'INFO', description: 'Show info and above (recommended)' },
      { value: 'WARN', label: 'WARN', description: 'Show warnings and errors only' },
      { value: 'ERROR', label: 'ERROR', description: 'Show errors only' },
      { value: 'FATAL', label: 'FATAL', description: 'Show only fatal errors' },
      { value: 'OFF', label: 'OFF', description: 'Disable all logging' },
    ];
  }
}

// Export singleton instance
export const logger = new Logger();

// Export default for convenient usage
export default logger;

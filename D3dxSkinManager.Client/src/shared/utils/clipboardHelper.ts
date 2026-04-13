import { notification } from './notification';

/**
 * Copy text to clipboard and show a success notification.
 * Handles errors silently (logs to console).
 */
export async function copyToClipboard(text: string, successMessage: string): Promise<void> {
  try {
    await navigator.clipboard.writeText(text);
    notification.success(successMessage);
  } catch {
    notification.error('Failed to copy to clipboard');
  }
}

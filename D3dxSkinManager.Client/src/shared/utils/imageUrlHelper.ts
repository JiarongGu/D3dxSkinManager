/**
 * Helper utilities for converting file paths to app:// scheme URLs
 * Used to load local images through custom scheme handler
 */

/**
 * Converts a file path to an app:// scheme URL
 * @param path - Relative or absolute file path, or existing URL
 * @returns app:// scheme URL or the original path if already a URL/data URI
 *
 * Examples:
 * - "profiles/123/thumbnails/abc.png" -> "app://profiles%2F123%2Fthumbnails%2Fabc.png"
 * - "http://example.com/image.png" -> "http://example.com/image.png" (unchanged)
 * - "data:image/png;base64,..." -> "data:image/png;base64,..." (unchanged)
 */
export function toAppUrl(path: string | null | undefined): string | null {
  if (!path) {
    return null;
  }

  // Already a full URL (http/https) - return as-is
  if (path.startsWith('http://') || path.startsWith('https://')) {
    return path;
  }

  // Already a data URI - return as-is
  if (path.startsWith('data:')) {
    return path;
  }

  // Already an app:// URL - return as-is
  if (path.startsWith('app://')) {
    return path;
  }

  // Convert file path to app:// URL
  // Note: Backend expects relative paths from data directory
  const encodedPath = encodeURIComponent(path);
  return `app://${encodedPath}`;
}

/**
 * Converts an array of file paths to app:// scheme URLs
 * @param paths - Array of file paths
 * @returns Array of app:// scheme URLs
 */
export function toAppUrls(paths: (string | null | undefined)[]): (string | null)[] {
  return paths.map(toAppUrl);
}

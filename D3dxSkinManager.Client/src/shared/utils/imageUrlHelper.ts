/**
 * Helper utilities for converting file paths to app:// scheme URLs
 * Used to load local images through custom scheme handler
 */

/**
 * Converts a file path to an app:// scheme URL
 * @param path - Relative or absolute file path, or existing URL
 * @param cacheTimestamp - Optional timestamp to append as query parameter for cache busting
 * @returns app:// scheme URL or the original path if already a URL/data URI
 *
 * Examples:
 * - "profiles/123/thumbnails/abc.png" -> "app://profiles%2F123%2Fthumbnails%2Fabc.png"
 * - "profiles/123/thumbnails/abc.png", 1234567890 -> "app://profiles%2F123%2Fthumbnails%2Fabc.png?t=1234567890"
 * - "http://example.com/image.png" -> "http://example.com/image.png" (unchanged)
 * - "data:image/png;base64,..." -> "data:image/png;base64,..." (unchanged)
 */
export function toAppUrl(path: string | undefined, cacheTimestamp?: number): string | undefined {
  if (!path) {
    return undefined;
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
  const baseUrl = `app://${encodedPath}`;

  // Append timestamp for cache busting if provided
  if (cacheTimestamp !== undefined) {
    return `${baseUrl}?t=${cacheTimestamp}`;
  }

  return baseUrl;
}

/**
 * proxy:// URL for a REMOTE image: the backend scheme handler fetches it into the GLOBAL on-demand
 * cache ({data}/remote-images) on first request — no preload IPC round-trip. A DEDICATED scheme so
 * the URL states its contract: app:// = local file, proxy:// = remote resource via the backend cache.
 * Non-http inputs are returned unchanged (already-local paths keep using toAppUrl).
 */
export function remoteImageUrl(url: string | undefined): string | undefined {
  if (!url) return undefined;
  if (!url.startsWith('http://') && !url.startsWith('https://')) return url;
  return `proxy://image/?u=${encodeURIComponent(url)}`;
}

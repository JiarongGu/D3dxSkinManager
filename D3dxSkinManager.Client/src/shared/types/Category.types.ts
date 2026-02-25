/**
 * Category representing a hierarchical category or character
 * Stored in SQLite and returned from backend API
 */
export interface CategoryInfo {
  /**
   * Unique identifier (GUID, e.g., "550e8400-e29b-41d4-a716-446655440000")
   */
  id: string;

  /**
   * Display name
   */
  name: string;

  /**
   * Parent category ID (undefined for root categories)
   */
  parentId?: string;

  /**
   * Thumbnail image URL (file:/// protocol)
   */
  thumbnail?: string;

  /**
   * Priority for sorting (higher = first)
   */
  priority: number;

  /**
   * Optional description
   */
  description?: string;

  /**
   * Match mode for auto-detection ("wildcard" or "regex")
   */
  matchMode?: string;

  /**
   * Match pattern for auto-detection
   */
  matchPattern?: string;

  /**
   * Additional metadata (JSON object)
   */
  metadata?: Record<string, string | number | boolean>;

  /**
   * Total number of mods in this category and all descendant categories
   */
  modCount?: number;

  /**
   * Child categories (subfolders or characters)
   */
  children: CategoryInfo[];
}

/**
 * Response from GET_CATEGORY_TREE IPC call
 */
export interface CategoryTreeResponse {
  data: CategoryInfo[];
}

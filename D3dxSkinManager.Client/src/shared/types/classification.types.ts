/**
 * Classification node representing a hierarchical category or character
 * Stored in SQLite and returned from backend API
 */
export interface ClassificationNode {
  /**
   * Unique identifier (e.g., "终末地" or "终末地/干员-物理/陈千语")
   */
  id: string;

  /**
   * Display name
   */
  name: string;

  /**
   * Parent node ID (null for root nodes)
   */
  parentId?: string | null;

  /**
   * Thumbnail image URL (file:/// protocol)
   */
  thumbnail?: string | null;

  /**
   * Priority for sorting (higher = first)
   */
  priority: number;

  /**
   * Optional description
   */
  description?: string | null;

  /**
   * Additional metadata (JSON object)
   */
  metadata?: Record<string, any> | null;

  /**
   * Total number of mods in this node and all descendant nodes
   */
  modCount?: number;

  /**
   * Child nodes (folders or characters)
   */
  children: ClassificationNode[];
}

/**
 * Response from GET_CLASSIFICATION_TREE IPC call
 */
export interface ClassificationTreeResponse {
  data: ClassificationNode[];
}

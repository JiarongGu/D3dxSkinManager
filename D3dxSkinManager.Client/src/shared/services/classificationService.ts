import { BaseModuleService } from './baseModuleService';
import type { ClassificationNode } from '../types/classification.types';

/**
 * Service for managing character classification tree
 * Communicates with backend ClassificationService via IPC
 */
class ClassificationService extends BaseModuleService {
  constructor() {
    super('MOD');
  }

  /**
   * Get the full classification tree from SQLite database
   * Returns hierarchical structure with thumbnails
   */
  async getClassificationTree(profileId: string): Promise<ClassificationNode[]> {
    return this.sendMessage<ClassificationNode[]>('GET_CLASSIFICATION_TREE', profileId);
  }

  /**
   * Find a classification node by ID (local tree search)
   * For validation, use nodeExists() instead which checks the database
   */
  findNodeById(tree: ClassificationNode[], id: string): ClassificationNode | undefined {
    for (const node of tree) {
      if (node.id === id) {
        return node;
      }
      if (node.children.length > 0) {
        const found = this.findNodeById(node.children, id);
        if (found) return found;
      }
    }
    return undefined;
  }

  /**
   * Check if a classification node exists in the database by nodeId (GUID)
   * Returns true if exists, false otherwise
   */
  async nodeExists(profileId: string, nodeId: string): Promise<boolean> {
    return this.sendMessage<boolean>('CHECK_CLASSIFICATION_NODE_EXISTS', profileId, { nodeId });
  }

  /**
   * Check if a classification name already exists in the database (case-insensitive)
   * Returns true if exists, false otherwise
   * Use this for form validation to prevent duplicate names
   * @param profileId - The profile ID
   * @param name - The classification name to check
   * @param excludeNodeId - Optional node ID to exclude from check (for edit validation)
   */
  async nameExists(profileId: string, name: string, excludeNodeId?: string): Promise<boolean> {
    return this.sendMessage<boolean>('CHECK_CLASSIFICATION_NAME_EXISTS', profileId, {
      name,
      excludeNodeId
    });
  }

  /**
   * Get all leaf nodes (characters) from tree
   */
  getAllLeafNodes(tree: ClassificationNode[]): ClassificationNode[] {
    const leaves: ClassificationNode[] = [];

    const traverse = (nodes: ClassificationNode[]) => {
      for (const node of nodes) {
        if (node.children.length === 0) {
          leaves.push(node);
        } else {
          traverse(node.children);
        }
      }
    };

    traverse(tree);
    return leaves;
  }

  /**
   * Flatten tree to list of all nodes
   */
  flattenTree(tree: ClassificationNode[]): ClassificationNode[] {
    const result: ClassificationNode[] = [];

    const traverse = (nodes: ClassificationNode[]) => {
      for (const node of nodes) {
        result.push(node);
        if (node.children.length > 0) {
          traverse(node.children);
        }
      }
    };

    traverse(tree);
    return result;
  }

  /**
   * Create a new classification node with auto-generated GUID
   * @param profileId - The profile ID
   * @param name - The display name of the node
   * @param parentId - The parent node ID (undefined for root level)
   * @param priority - Priority for sorting (default 100)
   * @param description - Optional description
   * @param thumbnail - Optional thumbnail path
   * @param matchMode - Optional match mode for auto-detection ("wildcard" or "regex")
   * @param matchPattern - Optional match pattern for auto-detection
   */
  async createNode(
    profileId: string,
    name: string,
    parentId?: string,
    priority?: number,
    description?: string,
    thumbnail?: string,
    matchMode?: string,
    matchPattern?: string
  ): Promise<ClassificationNode | undefined> {
    return this.sendMessage<ClassificationNode | undefined>(
      'CREATE_CLASSIFICATION_NODE',
      profileId,
      {
        name,
        parentId,
        priority: priority || 100,
        description,
        thumbnail,
        matchMode,
        matchPattern,
      }
    );
  }

  /**
   * Move a classification node to a new parent or position
   */
  async moveNode(
    profileId: string,
    nodeId: string,
    newParentId: string | undefined,
    dropPosition: number
  ): Promise<boolean> {
    return this.sendMessage<boolean>(
      'MOVE_CLASSIFICATION_NODE',
      profileId,
      {
        nodeId,
        newParentId: newParentId,
        dropPosition,
      }
    );
  }

  /**
   * Update a classification node's name, description, thumbnail, and auto-detection settings
   */
  async updateNode(
    profileId: string,
    nodeId: string,
    name: string,
    description?: string,
    icon?: string,
    matchMode?: string,
    matchPattern?: string
  ): Promise<boolean> {
    return this.sendMessage<boolean>(
      'UPDATE_CLASSIFICATION_NODE',
      profileId,
      { nodeId, name, description, icon, matchMode, matchPattern }
    );
  }

  /**
   * Delete a classification node
   */
  async deleteNode(profileId: string, nodeId: string): Promise<boolean> {
    return this.sendMessage<boolean>(
      'DELETE_CLASSIFICATION_NODE',
      profileId,
      { nodeId }
    );
  }
}

export const classificationService = new ClassificationService();

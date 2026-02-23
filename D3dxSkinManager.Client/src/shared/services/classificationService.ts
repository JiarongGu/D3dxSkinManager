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
   * Check if a classification node exists in the database by nodeId
   * Returns true if exists, false otherwise
   * Use this for validation to ensure data integrity
   */
  async nodeExists(profileId: string, nodeId: string): Promise<boolean> {
    return this.sendMessage<boolean>('CHECK_CLASSIFICATION_NODE_EXISTS', profileId, { nodeId });
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
   * @deprecated nodeId parameter - no longer needed, GUIDs are auto-generated
   */
  async createNode(
    profileId: string,
    nodeIdOrName: string, // For backward compatibility - if only 6 params, this is the name
    nameOrParentId?: string, // This becomes parentId if nodeId is omitted
    parentIdOrPriority?: string | number,
    priorityOrDescription?: number | string,
    descriptionOrThumbnail?: string,
    thumbnail?: string
  ): Promise<ClassificationNode | undefined> {
    // Handle both old signature (with nodeId) and new signature (without nodeId)
    let actualName: string;
    let actualParentId: string | undefined;
    let actualPriority: number;
    let actualDescription: string | undefined;
    let actualThumbnail: string | undefined;

    // Check if old signature is being used (7+ parameters)
    if (typeof nameOrParentId === 'string' && arguments.length >= 7) {
      // Old signature: createNode(profileId, nodeId, name, parentId, priority, description, thumbnail)
      actualName = nameOrParentId;
      actualParentId = parentIdOrPriority as string | undefined;
      actualPriority = priorityOrDescription as number || 100;
      actualDescription = descriptionOrThumbnail;
      actualThumbnail = thumbnail;
    } else {
      // New signature: createNode(profileId, name, parentId, priority, description, thumbnail)
      actualName = nodeIdOrName;
      actualParentId = nameOrParentId || undefined;
      actualPriority = typeof parentIdOrPriority === 'number' ? parentIdOrPriority : 100;
      actualDescription = typeof priorityOrDescription === 'string' ? priorityOrDescription : undefined;
      actualThumbnail = descriptionOrThumbnail;
    }

    return this.sendMessage<ClassificationNode | undefined>(
      'CREATE_CLASSIFICATION_NODE',
      profileId,
      {
        // nodeId is deprecated and will be ignored by backend
        name: actualName,
        parentId: actualParentId,
        priority: actualPriority,
        description: actualDescription,
        thumbnail: actualThumbnail,
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
   * Update a classification node's name, description, and thumbnail
   */
  async updateNode(
    profileId: string,
    nodeId: string,
    name: string,
    description?: string,
    icon?: string
  ): Promise<boolean> {
    return this.sendMessage<boolean>(
      'UPDATE_CLASSIFICATION_NODE',
      profileId,
      { nodeId, name, description, icon }
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

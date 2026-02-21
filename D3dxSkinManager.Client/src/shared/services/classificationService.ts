import { photinoService } from './photinoService';
import type { ClassificationNode } from '../types/classification.types';

/**
 * Service for managing character classification tree
 * Communicates with backend ClassificationService via IPC
 */
export const classificationService = {
  /**
   * Get the full classification tree from SQLite database
   * Returns hierarchical structure with thumbnails
   */
  async getClassificationTree(profileId: string): Promise<ClassificationNode[]> {
    return await photinoService.sendMessage<ClassificationNode[]>({ module: 'MOD', type: 'GET_CLASSIFICATION_TREE', profileId });
  },

  /**
   * Find a classification node by ID (local tree search)
   * For validation, use nodeExists() instead which checks the database
   */
  findNodeById(tree: ClassificationNode[], id: string): ClassificationNode | null {
    for (const node of tree) {
      if (node.id === id) {
        return node;
      }
      if (node.children.length > 0) {
        const found = this.findNodeById(node.children, id);
        if (found) return found;
      }
    }
    return null;
  },

  /**
   * Check if a classification node exists in the database by nodeId
   * Returns true if exists, false otherwise
   * Use this for validation to ensure data integrity
   */
  async nodeExists(profileId: string, nodeId: string): Promise<boolean> {
    return await photinoService.sendMessage<boolean>({
      module: 'MOD',
      type: 'CHECK_CLASSIFICATION_NODE_EXISTS',
      profileId,
      payload: { nodeId },
    });
  },

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
  },

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
  },

  /**
   * Create a new classification node
   */
  async createNode(
    profileId: string,
    nodeId: string,
    name: string,
    parentId: string | null = null,
    priority: number = 100,
    description?: string,
    thumbnail?: string
  ): Promise<ClassificationNode | null> {
    return await photinoService.sendMessage<ClassificationNode | null>({
      module: 'MOD',
      type: 'CREATE_CLASSIFICATION_NODE',
      profileId,
      payload: {
        nodeId,
        name,
        parentId,
        priority,
        description,
        thumbnail,
      },
    });
  },

  /**
   * Move a classification node to a new parent or position
   */
  async moveNode(
    profileId: string,
    nodeId: string,
    newParentId: string | null,
    dropPosition: number
  ): Promise<boolean> {
    return await photinoService.sendMessage<boolean>({
      module: 'MOD',
      type: 'MOVE_CLASSIFICATION_NODE',
      profileId,
      payload: {
        nodeId,
        newParentId,
        dropPosition,
      },
    });
  },

  /**
   * Update a classification node's name
   */
  async updateNode(profileId: string, nodeId: string, newName: string): Promise<boolean> {
    return await photinoService.sendMessage<boolean>({
      module: 'MOD',
      type: 'UPDATE_CLASSIFICATION_NODE',
      profileId,
      payload: { nodeId, newName },
    });
  },

  /**
   * Delete a classification node
   */
  async deleteNode(profileId: string, nodeId: string): Promise<boolean> {
    return await photinoService.sendMessage<boolean>({
      module: 'MOD',
      type: 'DELETE_CLASSIFICATION_NODE',
      profileId,
      payload: { nodeId },
    });
  }
};

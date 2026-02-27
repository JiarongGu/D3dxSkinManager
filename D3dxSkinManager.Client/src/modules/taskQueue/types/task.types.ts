/**
 * Task execution status
 */
export type TaskStatus = 'pending' | 'processing' | 'completed' | 'failed' | 'cancelled' | 'awaitingConfirmation';

/**
 * File type for import tasks
 */
export type FileType = 'archive' | 'folder';

/**
 * Task information from backend
 */
export interface TaskInfo {
  id: string;
  type: string;
  taskChainId: string;
  nodeId?: string;
  status: TaskStatus;
  progress: number;
  message?: string;
  createdAt: string;
  startedAt?: string;
  completedAt?: string;
  input: string;  // Renamed from inputData
  output?: string; // Renamed from outputData
  errorMessage?: string;
  operationId?: string;
}

/**
 * Task progress update
 */
export interface TaskProgress {
  taskId: string;
  progress: number;
  currentStep?: string;
  message?: string;
}

/**
 * Mod import task input
 */
export interface ModImportTaskInput {
  filePath: string;
  isFolder: boolean;
  name?: string;
  author?: string;
  description?: string;
  grading?: string;
  tags?: string[];
  category?: string;
}

/**
 * Mod import task output
 */
export interface ModImportTaskOutput {
  sha: string;
  name: string;
  success: boolean;
  errorMessage?: string;
}

/**
 * Compress folder task input
 */
export interface CompressFolderTaskInput {
  folderPath: string;
  outputPath?: string;
  compressionLevel?: number;
}

/**
 * Compress folder task output
 */
export interface CompressFolderTaskOutput {
  tempArchivePath: string;
  originalFolderPath: string;
  archiveSize: number;
  fileCount: number;
  metadata?: {
    folderName: string;
    createdAt: string;
    modifiedAt: string;
  };
}

/**
 * Import from temp task input
 */
export interface ImportFromTempTaskInput {
  tempArchivePath: string;
  originalFolderPath: string;
  name?: string;
  author?: string;
  description?: string;
  grading?: string;
  tags?: string[];
  category?: string;
}

/**
 * Task processor metadata - describes capabilities of a task processor
 */
export interface TaskProcessorMetadata {
  taskType: string;
  displayName: string;
  description: string;
  inputType: string;
  outputType: string;
  estimatedDurationSeconds?: number;
  supportsCancellation: boolean;
  supportsProgress: boolean;
  requiredFields?: string[];
  optionalFields?: string[];
  tags?: string[];
}

/**
 * Comparison operators for routing conditions
 */
export type ComparisonOperator =
  | 'equals' | 'notEquals'
  | 'greaterThan' | 'greaterThanOrEqual'
  | 'lessThan' | 'lessThanOrEqual'
  | 'contains' | 'notContains'
  | 'startsWith' | 'endsWith'
  | 'matches' | 'in' | 'notIn'
  | 'isNull' | 'isNotNull'
  | 'isEmpty' | 'isNotEmpty';

/**
 * Condition types for routing evaluation
 */
export type ConditionType =
  | 'taskStatus' | 'outputField' | 'sharedDataField'
  | 'hasError' | 'userInput'
  | 'and' | 'or' | 'not'
  | 'always' | 'custom';

/**
 * Routing condition for node transitions
 */
export interface RoutingCondition {
  type: ConditionType;
  field?: string;
  operator?: ComparisonOperator;
  value?: any;
  subConditions?: RoutingCondition[];
  customEvaluator?: string;
}

/**
 * Routing rule that determines next node
 */
export interface NodeRoutingRule {
  name?: string;
  condition: RoutingCondition;
  nextNodeId: string;
  priority?: number;
}

/**
 * Task chain node - a single step in a workflow graph
 */
export interface TaskChainNode {
  nodeId: string;
  taskType: string;
  inputMapping: Record<string, string>;
  outputMapping: Record<string, string>;
  routingRules: NodeRoutingRule[];
  defaultNextNode?: string;
  metadata?: Record<string, any>;
}

/**
 * Chain configuration - defines a task workflow graph
 */
export interface ChainConfiguration {
  chainId: string;
  chainType: string;
  startNodeId: string;
  nodes: TaskChainNode[];
}

/**
 * Task chain status
 */
export type TaskChainStatus =
  | 'pending' | 'processing'
  | 'completed' | 'failed' | 'cancelled';

/**
 * Task chain information
 */
export interface TaskChainInfo {
  id: string;
  chainType?: string;
  chainConfiguration?: string;
  status: TaskChainStatus;
  context?: string;
  input?: string;
  output?: string;
  errorMessage?: string;
  createdAt: string;
  updatedAt?: string;
  startedAt?: string;
  completedAt?: string;
}

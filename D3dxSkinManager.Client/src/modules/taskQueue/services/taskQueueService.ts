import { BaseModuleService } from '../../../shared/services/baseModuleService';
import type { TaskInfo, ModImportTaskInput } from '../types/task.types';

/**
 * Service for TaskQueue module IPC communication
 */
class TaskQueueService extends BaseModuleService {
  constructor() {
    super('TASK_QUEUE');
  }

  /**
   * Add a task to the queue
   */
  async addTask<TInput>(taskType: string, input: TInput, profileId?: string): Promise<string> {
    return this.sendMessage<string>('ADD_TASK', profileId, {
      taskType,
      input,
      profileId
    });
  }

  /**
   * Add a mod import task
   */
  async addModImportTask(input: ModImportTaskInput, profileId?: string): Promise<string> {
    return this.addTask('mod_import', input, profileId);
  }

  /**
   * Start processing the next pending task
   */
  async processNext(profileId?: string): Promise<void> {
    await this.sendMessage('PROCESS_NEXT', profileId);
  }

  /**
   * Cancel a running task
   */
  async cancelTask(taskId: string, profileId?: string): Promise<void> {
    await this.sendMessage('CANCEL_TASK', profileId, { taskId });
  }

  /**
   * Remove a task from queue
   */
  async removeTask(taskId: string, profileId?: string): Promise<void> {
    await this.sendMessage('REMOVE_TASK', profileId, { taskId });
  }

  /**
   * Get all tasks
   */
  async getAllTasks(profileId?: string): Promise<TaskInfo[]> {
    return this.sendArrayMessage<TaskInfo>('GET_ALL_TASKS', profileId);
  }

  /**
   * Get a single task by ID
   */
  async getTask(taskId: string, profileId?: string): Promise<TaskInfo | undefined> {
    return this.sendOptionalMessage<TaskInfo>('GET_TASK', profileId, { taskId });
  }

  /**
   * Clear completed and failed tasks
   */
  async clearCompleted(profileId?: string): Promise<void> {
    await this.sendMessage('CLEAR_COMPLETED', profileId);
  }

  /**
   * Continue a paused chain task with user input
   */
  async continueChain(correlationId: string, pausedTaskId: string, userInput?: Record<string, unknown>): Promise<string> {
    return this.sendMessage<string>('CONTINUE_CHAIN', undefined, {
      correlationId,
      pausedTaskId,
      userInput: userInput ? JSON.stringify(userInput) : undefined
    });
  }
}

export const taskQueueService = new TaskQueueService();

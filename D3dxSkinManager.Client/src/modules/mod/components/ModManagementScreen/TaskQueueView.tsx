import React, { useState, useEffect } from 'react';
import classNames from 'classnames';
import { List, Button, Space, Progress, Tag, Empty, Checkbox, Divider, Modal, Form, Input, Select } from 'antd';
import {
  PlayCircleOutlined,
  PauseCircleOutlined,
  DeleteOutlined,
  EditOutlined,
  CheckCircleOutlined,
  CloseCircleOutlined,
  ClockCircleOutlined,
  LoadingOutlined,
  FolderOpenOutlined,
  FileZipOutlined,
} from '@ant-design/icons';
import { taskQueueService } from '../../../taskQueue/services/taskQueueService';
import type { TaskInfo, TaskProgress, ModImportTaskInput } from '../../../taskQueue/types/task.types';
import { useProfile } from '../../../../shared/context/ProfileContext';
import { eventBus, TaskQueueEventType, Module } from '../../../../shared/services/eventBus';
import { systemService } from '../../../../shared/services/systemService';
import { notification } from '../../../../shared/utils/notification';
import { handleError } from '../../../../shared/utils/errorHandler';
import { useTranslation } from 'react-i18next';
import './TaskQueueView.css';

/**
 * Task Queue View - Download manager style interface for import tasks
 * Now powered by the backend TaskQueue module with real-time progress tracking
 */
export const TaskQueueView: React.FC = () => {
  const { t } = useTranslation();
  const { selectedProfileId } = useProfile();

  // Local state
  const [tasks, setTasks] = useState<TaskInfo[]>([]);
  const [selectedTaskIds, setSelectedTaskIds] = useState<string[]>([]);
  const [processing, setProcessing] = useState(false);
  const [metadataModalVisible, setMetadataModalVisible] = useState(false);
  const [awaitingTask, setAwaitingTask] = useState<TaskInfo | null>(null);

  /**
   * Load all tasks from backend
   */
  const loadTasks = async () => {
    try {
      const allTasks = await taskQueueService.getAllTasks(selectedProfileId);
      console.log('[TaskQueueView] Loaded tasks:', allTasks);
      setTasks(allTasks);
    } catch (error) {
      handleError(error);
    }
  };

  /**
   * Initial load
   */
  useEffect(() => {
    void loadTasks();
  }, []);

  /**
   * Listen to task events for real-time updates
   * Using .subscribe() to receive full Event object with payload
   */
  useEffect(() => {
    const unsubscribeAdded = eventBus.subscribe(Module.TASK_QUEUE, TaskQueueEventType.ADDED, (event) => {
      console.log('[TaskQueueView] TaskAdded event received:', event?.payload);
      if (event?.payload) {
        // Add task to state directly instead of reloading all
        setTasks(prev => [...prev, event.payload!]);
      }
    });

    const unsubscribeStarted = eventBus.subscribe(Module.TASK_QUEUE, TaskQueueEventType.STARTED, (event) => {
      console.log('[TaskQueueView] TaskStarted event received:', event?.payload);
      if (event?.payload) {
        // Update task status to processing
        setTasks(prev => prev.map(t =>
          t.id === event.payload!.id
            ? { ...event.payload! }
            : t
        ));
        setProcessing(true);
      }
    });

    const unsubscribeProgress = eventBus.subscribe(Module.TASK_QUEUE, TaskQueueEventType.PROGRESS, (event) => {
      console.log('[TaskQueueView] TaskProgress event received:', event?.payload);
      if (!event?.payload) return;
      // Update specific task progress in local state
      setTasks(prev => prev.map(t =>
        t.id === event.payload!.taskId
          ? { ...t, progress: event.payload!.progress, message: event.payload!.message }
          : t
      ));
    });

    const unsubscribeCompleted = eventBus.subscribe(Module.TASK_QUEUE, TaskQueueEventType.COMPLETED, (event) => {
      console.log('[TaskQueueView] TaskCompleted event received:', event?.payload);
      if (event?.payload) {
        // Update task with completed data
        setTasks(prev => prev.map(t =>
          t.id === event.payload!.id
            ? { ...event.payload! }
            : t
        ));
        setProcessing(false);
      }
    });

    const unsubscribeFailed = eventBus.subscribe(Module.TASK_QUEUE, TaskQueueEventType.FAILED, (event) => {
      console.log('[TaskQueueView] TaskFailed event received:', event?.payload);
      if (event?.payload) {
        // Update task with failed data
        setTasks(prev => prev.map(t =>
          t.id === event.payload!.id
            ? { ...event.payload! }
            : t
        ));
        setProcessing(false);
      }
    });

    const unsubscribeCancelled = eventBus.subscribe(Module.TASK_QUEUE, TaskQueueEventType.CANCELLED, (event) => {
      console.log('[TaskQueueView] TaskCancelled event received:', event?.payload);
      if (event?.payload) {
        // Update task with cancelled data
        setTasks(prev => prev.map(t =>
          t.id === event.payload!.id
            ? { ...event.payload! }
            : t
        ));
        setProcessing(false);
      }
    });

    const unsubscribeRemoved = eventBus.subscribe(Module.TASK_QUEUE, TaskQueueEventType.REMOVED, (event) => {
      console.log('[TaskQueueView] TaskRemoved event received:', event?.payload);
      if (event?.payload) {
        // Remove task from state
        setTasks(prev => prev.filter(t => t.id !== event.payload!.id));
      }
    });

    const unsubscribeAwaitingConfirmation = eventBus.subscribe(Module.TASK_QUEUE, TaskQueueEventType.AWAITING_CONFIRMATION, (event) => {
      console.log('[TaskQueueView] TaskAwaitingConfirmation event received:', event?.payload);
      if (event?.payload) {
        // Update task status
        setTasks(prev => prev.map(t =>
          t.id === event.payload!.id
            ? { ...event.payload! }
            : t
        ));
        // Show metadata input modal
        setAwaitingTask(event.payload);
        setMetadataModalVisible(true);
      }
    });

    return () => {
      unsubscribeAdded();
      unsubscribeStarted();
      unsubscribeProgress();
      unsubscribeCompleted();
      unsubscribeFailed();
      unsubscribeCancelled();
      unsubscribeRemoved();
      unsubscribeAwaitingConfirmation();
    };
  }, []);

  /**
   * Calculate task statistics
   */
  const stats = {
    total: tasks.length,
    pending: tasks.filter(t => t.status === 'pending').length,
    processing: tasks.filter(t => t.status === 'processing').length,
    completed: tasks.filter(t => t.status === 'completed').length,
    failed: tasks.filter(t => t.status === 'failed').length,
  };

  /**
   * Open file/folder selection dialog
   */
  const handleSelectFiles = async () => {
    if (!selectedProfileId) {
      notification.error(t('mods.notifications.noProfileSelected'));
      return;
    }

    Modal.confirm({
      title: t('importQueue.selectImportSource'),
      content: t('importQueue.selectSourceDescription'),
      okText: t('importQueue.selectArchiveFiles'),
      cancelText: t('importQueue.selectFolder'),
      onOk: () => {
        // Close modal first, then show file dialog after a small delay
        // This prevents threading issues with WebView2/Windows dialogs
        setTimeout(async () => {
          try {
            const result = await systemService.openFileDialog({
              title: t('importQueue.selectArchives'),
              multiSelect: true,
              filters: [
                { name: t('importQueue.archiveFiles'), extensions: ['zip', 'rar', '7z', 'tar', 'gz', 'bz2'] },
                { name: t('importQueue.allFiles'), extensions: ['*'] }
              ],
              rememberPathKey: 'mod_import'
            });

            console.log('[TaskQueueView] File dialog result:', result);
            if (result.success && result.filePath) {
              const filePaths = result.filePath.split('\n').filter(p => p.trim());
              console.log('[TaskQueueView] Selected file paths:', filePaths);
              await createTasksFromFiles(filePaths, false);
            } else {
              console.warn('[TaskQueueView] No file selected or dialog cancelled');
            }
          } catch (error) {
            handleError(error);
          }
        }, 100);
      },
      onCancel: () => {
        // Close modal first, then show folder dialog after a small delay
        // This prevents threading issues with WebView2/Windows dialogs
        setTimeout(async () => {
          try {
            const result = await systemService.openFolderDialog({
              title: t('importQueue.selectModFolder'),
              rememberPathKey: 'mod-import-task'
            });

            console.log('[TaskQueueView] Folder dialog result:', result);
            if (result.success && result.filePath) {
              console.log('[TaskQueueView] Selected folder:', result.filePath);
              await createTasksFromFiles([result.filePath], true);
            } else {
              console.warn('[TaskQueueView] No folder selected or dialog cancelled');
            }
          } catch (error) {
            handleError(error);
          }
        }, 100);
      }
    });
  };

  /**
   * Create import tasks from selected files/folders
   */
  const createTasksFromFiles = async (paths: string[], isFolder: boolean) => {
    try {
      console.log('[TaskQueueView] Creating tasks for paths:', paths, 'isFolder:', isFolder);
      for (const filePath of paths) {
        // Extract filename for default name
        const fileName = filePath.split(/[\\/]/).pop() || 'Unknown';
        const nameWithoutExt = fileName.replace(/\.(zip|rar|7z|tar|gz|bz2)$/i, '');

        const input: ModImportTaskInput = {
          filePath,
          isFolder,
          name: nameWithoutExt,
          grading: 'G',
          tags: [],
        };

        console.log('[TaskQueueView] Adding task with input:', JSON.stringify(input, null, 2));
        const taskId = await taskQueueService.addModImportTask(input, selectedProfileId);
        console.log('[TaskQueueView] Task added with ID:', taskId);
      }

      notification.success(t('importQueue.tasksAdded', { count: paths.length }));
      console.log('[TaskQueueView] All tasks added, reloading...');
      await loadTasks();

      // Automatically start processing (force start without checking stats)
      console.log('[TaskQueueView] Starting task processing...');
      try {
        setProcessing(true);
        await taskQueueService.processNext(selectedProfileId);
      } catch (error) {
        console.error('[TaskQueueView] Failed to start processing:', error);
        setProcessing(false);
      }
    } catch (error) {
      handleError(error);
    }
  };

  /**
   * Start processing all pending tasks
   */
  const handleStartAll = async () => {
    if (stats.pending === 0 || processing) return;

    try {
      setProcessing(true);
      await taskQueueService.processNext(selectedProfileId);
    } catch (error) {
      handleError(error);
      setProcessing(false);
    }
  };

  /**
   * Pause processing
   */
  const handlePause = async () => {
    // For now, just update local state
    // Backend will finish current task then stop
    setProcessing(false);
  };

  /**
   * Remove selected tasks
   */
  const handleRemoveSelected = async () => {
    if (selectedTaskIds.length === 0) return;

    try {
      for (const taskId of selectedTaskIds) {
        await taskQueueService.removeTask(taskId, selectedProfileId);
      }
      setSelectedTaskIds([]);
    } catch (error) {
      handleError(error);
    }
  };

  /**
   * Clear completed tasks
   */
  const handleClearCompleted = async () => {
    try {
      await taskQueueService.clearCompleted(selectedProfileId);
      setSelectedTaskIds([]);
    } catch (error) {
      handleError(error);
    }
  };

  /**
   * Get status icon
   */
  const getStatusIcon = (status: string) => {
    switch (status) {
      case 'pending':
        return <ClockCircleOutlined style={{ color: '#8c8c8c' }} />;
      case 'processing':
        return <LoadingOutlined style={{ color: '#1890ff' }} />;
      case 'completed':
        return <CheckCircleOutlined style={{ color: '#52c41a' }} />;
      case 'failed':
        return <CloseCircleOutlined style={{ color: '#ff4d4f' }} />;
      case 'cancelled':
        return <CloseCircleOutlined style={{ color: '#8c8c8c' }} />;
      case 'awaitingConfirmation':
        return <EditOutlined style={{ color: '#fa8c16' }} />;
      default:
        return <ClockCircleOutlined />;
    }
  };

  /**
   * Get status tag
   */
  const getStatusTag = (status: string) => {
    const colorMap: Record<string, string> = {
      pending: 'default',
      processing: 'processing',
      completed: 'success',
      failed: 'error',
      cancelled: 'default',
      awaitingConfirmation: 'warning',
    };

    return (
      <Tag color={colorMap[status] || 'default'}>
        {t(`importQueue.status.${status}`)}
      </Tag>
    );
  };

  /**
   * Handle metadata submission for awaiting confirmation task
   */
  const handleMetadataSubmit = async (metadata: Record<string, unknown>) => {
    if (!awaitingTask) return;

    try {
      console.log('[TaskQueueView] Submitting metadata for task:', awaitingTask.id, metadata);

      // Continue the chain with user input
      const nextTaskId = await taskQueueService.continueChain(
        awaitingTask.taskChainId,
        awaitingTask.id,
        metadata
      );

      console.log('[TaskQueueView] Chain continued with new task:', nextTaskId);
      notification.success(t('importQueue.metadataSubmitted'));

      // Close modal
      setMetadataModalVisible(false);
      setAwaitingTask(null);

      // Reload tasks
      await loadTasks();

      // Continue processing
      setProcessing(true);
      await taskQueueService.processNext(selectedProfileId);
    } catch (error) {
      handleError(error);
    }
  };

  /**
   * Parse task input to get file name
   */
  const getTaskFileName = (task: TaskInfo): string => {
    try {
      console.log('[TaskQueueView] Parsing task input:', task.id, task.type, task.input);
      const input = JSON.parse(task.input);
      console.log('[TaskQueueView] Parsed input:', input);

      // Handle different task types
      let filePath: string | undefined;

      if (task.type === 'compress_folder') {
        filePath = input.folderPath;
      } else if (task.type === 'mod_import' || task.type === 'import_from_temp') {
        filePath = input.filePath || input.tempArchivePath;
      }

      if (!filePath) {
        console.warn('[TaskQueueView] No file path found in task input');
        return input.name || 'Unknown';
      }

      const fileName = filePath.split(/[\\/]/).pop() || 'Unknown';
      console.log('[TaskQueueView] Extracted filename:', fileName);
      return fileName;
    } catch (error) {
      console.error('[TaskQueueView] Failed to parse task input:', error, task);
      return 'Unknown';
    }
  };

  return (
    <div className="task-queue-view">
      {/* Status Bar */}
      <div className="task-queue-status-bar">
        <Space separator={<Divider orientation="vertical" />} size={"small"}>
          <span>{t('importQueue.stats.total')}: <strong>{stats.total}</strong></span>
          <span>{t('importQueue.stats.pending')}: <strong>{stats.pending}</strong></span>
          <span>{t('importQueue.stats.processing')}: <strong>{stats.processing}</strong></span>
          <span>{t('importQueue.stats.completed')}: <strong style={{ color: '#52c41a' }}>{stats.completed}</strong></span>
          <span>{t('importQueue.stats.failed')}: <strong style={{ color: '#ff4d4f' }}>{stats.failed}</strong></span>
        </Space>
      </div>

      {/* Toolbar */}
      <div className="task-queue-toolbar">
        <Space wrap>
          <Button
            type="primary"
            icon={<FolderOpenOutlined />}
            onClick={handleSelectFiles}
            disabled={processing}
          >
            {t('importQueue.selectFiles')}
          </Button>
          <Divider type="vertical" />
          <Button
            type="primary"
            icon={<PlayCircleOutlined />}
            onClick={handleStartAll}
            disabled={processing || stats.pending === 0}
          >
            {t('importQueue.startAll', { count: stats.pending })}
          </Button>
          <Button
            icon={<PauseCircleOutlined />}
            onClick={handlePause}
            disabled={!processing}
          >
            {t('importQueue.pause')}
          </Button>
          <Divider type="vertical" />
          <Button
            danger
            icon={<DeleteOutlined />}
            onClick={handleRemoveSelected}
            disabled={selectedTaskIds.length === 0 || processing}
          >
            {t('importQueue.removeSelected', { count: selectedTaskIds.length })}
          </Button>
          <Button
            onClick={handleClearCompleted}
            disabled={stats.completed === 0 && stats.failed === 0}
          >
            {t('importQueue.clearCompleted')}
          </Button>
        </Space>
      </div>

      {/* Task List */}
      {tasks.length === 0 ? (
        <Empty
          image={Empty.PRESENTED_IMAGE_SIMPLE}
          description={t('importQueue.noTasks')}
        />
      ) : (
        <List
          className="task-queue-list"
          dataSource={tasks}
          renderItem={(task) => (
            <List.Item
              key={task.id}
              className={classNames('task-item', `task-item-${task.status}`)}
            >
              <div className="task-item-content">
                <Checkbox
                  checked={selectedTaskIds.includes(task.id)}
                  onChange={(e) => {
                    if (e.target.checked) {
                      setSelectedTaskIds([...selectedTaskIds, task.id]);
                    } else {
                      setSelectedTaskIds(selectedTaskIds.filter(id => id !== task.id));
                    }
                  }}
                  disabled={task.status === 'processing'}
                />

                <div className="task-info">
                  <div className="task-header">
                    {getStatusIcon(task.status)}
                    <span className="task-name">{getTaskFileName(task)}</span>
                    {getStatusTag(task.status)}
                  </div>

                  {task.status === 'processing' && (
                    <Progress
                      percent={task.progress}
                      status="active"
                      strokeColor="#1890ff"
                      size="small"
                    />
                  )}

                  {task.message && (
                    <div className="task-message">{task.message}</div>
                  )}

                  {task.errorMessage && (
                    <div className="task-error">{task.errorMessage}</div>
                  )}
                </div>
              </div>
            </List.Item>
          )}
        />
      )}

      {/* Metadata Input Modal for Awaiting Confirmation */}
      {awaitingTask && (
        <MetadataInputModal
          visible={metadataModalVisible}
          task={awaitingTask}
          onSubmit={handleMetadataSubmit}
          onCancel={() => {
            setMetadataModalVisible(false);
            setAwaitingTask(null);
          }}
        />
      )}
    </div>
  );
};

/**
 * Metadata Input Modal Component
 */
interface MetadataInputModalProps {
  visible: boolean;
  task: TaskInfo;
  onSubmit: (metadata: Record<string, unknown>) => void;
  onCancel: () => void;
}

const MetadataInputModal: React.FC<MetadataInputModalProps> = ({ visible, task, onSubmit, onCancel }) => {
  const { t } = useTranslation();
  const [form] = Form.useForm();
  const { TextArea } = Input;

  // Parse chain context to get initial values
  const getInitialValues = () => {
    try {
      // Parse the task's output or input data to get chain context
      const taskData = task.output ? JSON.parse(task.output) : {};
      return {
        name: taskData.metadata_name || '',
        author: taskData.metadata_author || '',
        description: taskData.metadata_description || '',
        grading: taskData.metadata_grading || 'G',
        category: taskData.metadata_category || '',
        tags: taskData.metadata_tags || [],
      };
    } catch (error) {
      console.error('Failed to parse task data:', error);
      return {
        name: '',
        author: '',
        description: '',
        grading: 'G',
        category: '',
        tags: [],
      };
    }
  };

  useEffect(() => {
    if (visible) {
      form.setFieldsValue(getInitialValues());
    }
  }, [visible, task]);

  const handleSubmit = () => {
    form.validateFields().then((values) => {
      onSubmit(values);
      form.resetFields();
    });
  };

  const ageRatingOptions = [
    { value: 'G', label: t('ageRating.general') },
    { value: 'P', label: t('ageRating.parentalGuidance') },
    { value: 'R', label: t('ageRating.restricted') },
    { value: 'X', label: t('ageRating.adultsOnly') },
  ];

  return (
    <Modal
      title={t('importQueue.metadataInput.title')}
      open={visible}
      onOk={handleSubmit}
      onCancel={onCancel}
      width={600}
      okText={t('common.continue')}
      cancelText={t('common.cancel')}
    >
      <Form
        form={form}
        layout="vertical"
      >
        <Form.Item
          name="name"
          label={t('modEditor.name')}
          rules={[{ required: true, message: t('modEditor.nameRequired') }]}
        >
          <Input placeholder={t('modEditor.namePlaceholder')} />
        </Form.Item>

        <Form.Item
          name="author"
          label={t('modEditor.author')}
        >
          <Input placeholder={t('modEditor.authorPlaceholder')} />
        </Form.Item>

        <Form.Item
          name="description"
          label={t('modEditor.description')}
        >
          <TextArea
            rows={3}
            placeholder={t('modEditor.descriptionPlaceholder')}
          />
        </Form.Item>

        <Form.Item
          name="grading"
          label={t('modEditor.grading')}
        >
          <Select options={ageRatingOptions} />
        </Form.Item>

        <Form.Item
          name="category"
          label={t('modEditor.category')}
          rules={[{ required: true, message: t('modEditor.categoryRequired') }]}
        >
          <Input placeholder={t('modEditor.categoryPlaceholder')} />
        </Form.Item>

        <Form.Item
          name="tags"
          label={t('modEditor.tags')}
        >
          <Select
            mode="tags"
            placeholder={t('modEditor.tagsPlaceholder')}
          />
        </Form.Item>
      </Form>
    </Modal>
  );
};

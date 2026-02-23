import { notification } from '../../../../shared/utils/notification';
import React, { useState, useEffect, useRef } from 'react';
import { Form, Space, Row, Col } from 'antd';
import { FolderOpenOutlined } from '@ant-design/icons';
import { ClassificationNode } from '../../../../shared/types/classification.types';
import { useSlideInScreen } from '../../../../shared/context/SlideInScreenContext';
import { systemService } from '../../../../shared/services/systemService';
import { toAppUrl } from '../../../../shared/utils/imageUrlHelper';
import { classificationService } from '../../../../shared/services/classificationService';
import { useProfile } from '../../../../shared/context/ProfileContext';
import { CompactInput, CompactTextArea, CompactSelect, CompactButton, CompactPrimaryButton, CompactUpload } from '../../../../shared/components/compact';
import { useTranslation } from 'react-i18next';
import './ClassificationScreen.css';

interface ClassificationScreenProps {
  /**
   * Parent node ID (undefined for root classification)
   */
  parentId?: string;

  /**
   * Classification tree for parent selection
   */
  tree: ClassificationNode[];

  /**
   * Node to edit (if editing existing classification)
   */
  editNode?: ClassificationNode;

  /**
   * Callback when classification is saved
   */
  onSave: (data: {
    name: string;
    parentId?: string;
    thumbnail?: string;
    description?: string;
  }) => Promise<void>;
}

/**
 * Flatten tree to get all nodes for parent selection
 */
function flattenTree(nodes: ClassificationNode[]): ClassificationNode[] {
  const result: ClassificationNode[] = [];

  const traverse = (node: ClassificationNode) => {
    result.push(node);
    node.children.forEach(child => traverse(child));
  };

  nodes.forEach(node => traverse(node));
  return result;
}

/**
 * Content component for classification creation/editing
 */
export const ClassificationScreenContent: React.FC<ClassificationScreenProps & { screenId: string }> = ({
  parentId,
  tree,
  editNode,
  onSave,
  screenId
}) => {
  const { t } = useTranslation();
  const [form] = Form.useForm();
  const [loading, setLoading] = useState(false);
  const [thumbnailPath, setThumbnailPath] = useState<string>();
  const [thumbnailFileName, setThumbnailFileName] = useState<string>();
  const { closeScreen } = useSlideInScreen();
  const { selectedProfileId } = useProfile();
  const dropZoneRef = useRef<HTMLDivElement>(null);

  // Handle file drops - convert File object to real path via backend
  const handleDropThumbnail = async (file: File, filePath?: string) => {
    try {
      console.log('[ClassificationScreen] File dropped:', file);

      // Check if it's an image file
      const imageExtensions = ['.png', '.jpg', '.jpeg', '.gif', '.bmp', '.webp'];
      const ext = file.name.toLowerCase().match(/\.[^.]+$/)?.[0];

      if (!ext || !imageExtensions.includes(ext)) {
        notification.warning(t('classification.screen.dropImageOnly'));
        return;
      }

      // Convert File to base64 and send to backend to save as temp file
      const reader = new FileReader();
      reader.onload = async () => {
        try {
          const base64Data = (reader.result as string).split(',')[1]; // Remove data:image/jpeg;base64, prefix

          // TODO: Call backend to save temp file and get real path
          // For now, create object URL as fallback
          const objectUrl = URL.createObjectURL(file);
          setThumbnailPath(objectUrl);
          setThumbnailFileName(file.name);

          console.log('[ClassificationScreen] Using object URL for preview:', objectUrl);
        } catch (error) {
          console.error('[ClassificationScreen] Error processing file:', error);
          notification.error(t('classification.screen.dropImageFailed'));
        }
      };

      reader.readAsDataURL(file);
    } catch (error: unknown) {
      const errorMessage = error instanceof Error ? error.message : 'Unknown error';
      notification.error(`${t('classification.screen.selectThumbnailFailed')}: ${errorMessage}`);
      console.error('[ClassificationScreen] Failed to set thumbnail:', error);
    }
  };

  // Initialize form
  useEffect(() => {
    form.resetFields();

    if (editNode) {
      // Edit mode - populate with existing node data
      form.setFieldsValue({
        name: editNode.name,
        parentId: editNode.parentId || '',
        description: editNode.description
      });
      setThumbnailPath(editNode.thumbnail || undefined);
      if (editNode.thumbnail) {
        const fileName = editNode.thumbnail.split(/[\\/]/).pop() || editNode.thumbnail;
        setThumbnailFileName(fileName);
      }
    } else {
      // Create mode - clear and set parent if provided
      setThumbnailPath(undefined);
      setThumbnailFileName(undefined);
      if (parentId) {
        form.setFieldsValue({ parentId });
      }
    }
  }, [parentId, editNode, form]);

  const handleSubmit = async () => {
    try {
      const values = await form.validateFields();
      setLoading(true);

      await onSave({
        name: values.name,
        parentId: values.parentId,
        thumbnail: thumbnailPath,
        description: values.description
      });

      notification.success(t('classification.screen.saved'));
      closeScreen(screenId);
    } catch (error: any) {
      if (error.errorFields) {
        // Form validation error
        return;
      }
      console.error('Failed to save classification:', error);
      notification.error(t('classification.screen.saveFailed'));
    } finally {
      setLoading(false);
    }
  };

  const handleCancel = () => {
    closeScreen(screenId);
  };

  const handleBrowseThumbnail = async () => {
    try {
      const result = await systemService.openFileDialog({
        title: t('classification.screen.selectThumbnailTitle'),
        filters: [
          { name: t('classification.screen.imageFiles'), extensions: ['png', 'jpg', 'jpeg', 'gif', 'bmp', 'webp'] }
        ],
        rememberPathKey: 'classificationThumbnail'
      });

      if (result.success && result.filePath) {
        setThumbnailPath(result.filePath);
        const fileName = result.filePath.split(/[\\/]/).pop() || result.filePath;
        setThumbnailFileName(fileName);
      }
    } catch (error: unknown) {
      const errorMessage = error instanceof Error ? error.message : 'Unknown error';
      notification.error(`${t('classification.screen.selectThumbnailFailed')}: ${errorMessage}`);
    }
  };

  // handleDropThumbnail removed - now using OS-level drop via useOSFileDrop hook

  const allNodes = flattenTree(tree);

  return (
    <div>
      <Form
        form={form}
        layout="vertical"
        initialValues={{
          parentId: parentId
        }}
      >
        {/* Compact row: Name and Parent side by side */}
        <Row gutter={16}>
          <Col span={12}>
            <Form.Item
              name="name"
              label={t('classification.screen.nameLabel')}
              rules={[
                { required: true, message: t('classification.screen.nameRequired') },
                { min: 1, max: 100, message: t('classification.screen.nameLength') },
                ...(!editNode ? [{
                  validator: async (_: any, value: string) => {
                    if (!value || !selectedProfileId) return Promise.resolve();
                    // Check if nodeId already exists in database (name is used as nodeId in creation)
                    const exists = await classificationService.nodeExists(selectedProfileId, value);
                    if (exists) {
                      return Promise.reject(t('classification.screen.nameExists'));
                    }
                    return Promise.resolve();
                  }
                }] : [])
              ]}
            >
              <CompactInput
                placeholder={t('classification.screen.namePlaceholder')}
                autoFocus
              />
            </Form.Item>
          </Col>
          <Col span={12}>
            <Form.Item
              name="parentId"
              label={t('classification.screen.parentLabel')}
            >
              <CompactSelect
                placeholder={t('classification.screen.parentPlaceholder')}
                allowClear
                showSearch
                filterOption={(input, option) =>
                  (option?.label?.toString().toLowerCase() ?? '').includes(input.toLowerCase())
                }
                options={[
                  { value: '', label: t('classification.screen.rootNoParent') },
                  ...allNodes.map(node => ({
                    value: node.id,
                    label: node.name
                  }))
                ]}
              />
            </Form.Item>
          </Col>
        </Row>

        {/* Compact description - smaller textarea */}
        <Form.Item
          name="description"
          label={t('classification.screen.descriptionLabel')}
        >
          <CompactTextArea
            placeholder={t('classification.screen.descriptionPlaceholder')}
            rows={3}
            maxLength={500}
            showCount
          />
        </Form.Item>

        {/* Thumbnail with drag-drop area or preview */}
        <Form.Item
          label={t('classification.screen.thumbnailLabel')}
        >
          {!thumbnailPath ? (
            // Drag-drop area when no image selected - with OS-level drop zone
            <div ref={dropZoneRef}>
              <CompactUpload
                onSelect={handleBrowseThumbnail}
                onDrop={handleDropThumbnail}
                title={t('classification.screen.thumbnailUploadTitle')}
                subtitle={t('classification.screen.thumbnailUploadSubtitle')}
              />
            </div>
          ) : (
            // Image preview when selected
            <div className="classification-screen-thumbnail-container">
              <div className="classification-screen-thumbnail-preview">
                <img
                  src={toAppUrl(thumbnailPath) || undefined}
                  alt={t('classification.screen.thumbnailPreview')}
                  className="classification-screen-thumbnail-image"
                  onError={(e) => {
                    (e.target as HTMLImageElement).src = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
                  }}
                />
              </div>
              <div className="classification-screen-thumbnail-info">
                <div className="classification-screen-thumbnail-filename">
                  {thumbnailFileName}
                </div>
                <Space size={4}>
                  <CompactButton
                    icon={<FolderOpenOutlined />}
                    onClick={handleBrowseThumbnail}
                    size="small"
                  >
                    {t('classification.screen.changeButton')}
                  </CompactButton>
                  <CompactButton
                    danger
                    onClick={() => {
                      setThumbnailPath(undefined);
                      setThumbnailFileName(undefined);
                    }}
                    size="small"
                  >
                    {t('classification.screen.removeButton')}
                  </CompactButton>
                </Space>
              </div>
            </div>
          )}
        </Form.Item>
      </Form>

      <div className="slide-in-screen-footer">
        <Space>
          <CompactButton onClick={handleCancel} size="large">
            {t('classification.screen.cancelButton')}
          </CompactButton>
          <CompactPrimaryButton
            onClick={handleSubmit}
            loading={loading}
            size="large"
          >
            {t('classification.screen.saveButton')}
          </CompactPrimaryButton>
        </Space>
      </div>
    </div>
  );
};

/**
 * Hook to open classification screen
 */
export function useClassificationScreen() {
  const { openScreen } = useSlideInScreen();
  const { t } = useTranslation();

  const openClassificationScreen = (props: ClassificationScreenProps) => {
    // Create a wrapper that will receive the screenId
    let actualScreenId = '';

    const ContentWrapper = () => (
      <ClassificationScreenContent {...props} screenId={actualScreenId} />
    );

    const title = props.editNode ? t('classification.screen.title.edit') : t('classification.screen.title.add');

    actualScreenId = openScreen({
      title,
      width: '50%',
      content: <ContentWrapper />,
    });

    return actualScreenId;
  };

  return { openClassificationScreen };
}

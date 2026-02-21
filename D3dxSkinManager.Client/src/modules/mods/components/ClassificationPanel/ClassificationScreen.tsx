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
  onSave,
  screenId
}) => {
  const [form] = Form.useForm();
  const [loading, setLoading] = useState(false);
  const [thumbnailPath, setThumbnailPath] = useState<string | undefined>();
  const [thumbnailFileName, setThumbnailFileName] = useState<string | undefined>();
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
        notification.warning('Please drop an image file');
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
          notification.error('Failed to process dropped file');
        }
      };

      reader.readAsDataURL(file);
    } catch (error: unknown) {
      const errorMessage = error instanceof Error ? error.message : 'Unknown error';
      notification.error(`Failed to set thumbnail: ${errorMessage}`);
      console.error('[ClassificationScreen] Failed to set thumbnail:', error);
    }
  };

  // Initialize form
  useEffect(() => {
    form.resetFields();
    setThumbnailPath(undefined);
    setThumbnailFileName(undefined);

    // Set parent if provided
    if (parentId) {
      form.setFieldsValue({ parentId });
    }
  }, [parentId, form]);

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

      notification.success('Classification saved successfully');
      closeScreen(screenId);
    } catch (error: any) {
      if (error.errorFields) {
        // Form validation error
        return;
      }
      console.error('Failed to save classification:', error);
      notification.error('Failed to save classification');
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
        title: 'Select Classification Thumbnail',
        filters: [
          { name: 'Image Files', extensions: ['png', 'jpg', 'jpeg', 'gif', 'bmp', 'webp'] }
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
      notification.error(`Failed to select thumbnail: ${errorMessage}`);
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
              label="Classification Name"
              rules={[
                { required: true, message: 'Please enter a classification name' },
                { min: 1, max: 100, message: 'Name must be between 1 and 100 characters' },
                {
                  validator: async (_, value) => {
                    if (!value || !selectedProfileId) return Promise.resolve();
                    // Check if nodeId already exists in database (name is used as nodeId in creation)
                    const exists = await classificationService.nodeExists(selectedProfileId, value);
                    if (exists) {
                      return Promise.reject('A classification with this name already exists. Please use a different name.');
                    }
                    return Promise.resolve();
                  }
                }
              ]}
            >
              <CompactInput
                placeholder="e.g., Character, Weapon, Outfit"
                autoFocus
              />
            </Form.Item>
          </Col>
          <Col span={12}>
            <Form.Item
              name="parentId"
              label="Parent Classification"
            >
              <CompactSelect
                placeholder="Root (No Parent)"
                allowClear
                showSearch
                filterOption={(input, option) =>
                  (option?.label?.toString().toLowerCase() ?? '').includes(input.toLowerCase())
                }
                options={[
                  { value: '', label: '(Root - No Parent)' },
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
          label="Description (Optional)"
        >
          <CompactTextArea
            placeholder="Optional description"
            rows={3}
            maxLength={500}
            showCount
          />
        </Form.Item>

        {/* Thumbnail with drag-drop area or preview */}
        <Form.Item
          label="Thumbnail (Optional)"
        >
          {!thumbnailPath ? (
            // Drag-drop area when no image selected - with OS-level drop zone
            <div ref={dropZoneRef}>
              <CompactUpload
                onSelect={handleBrowseThumbnail}
                onDrop={handleDropThumbnail}
                title="Click or drag to select image file"
                subtitle="PNG, JPG, JPEG, GIF, BMP, WEBP"
              />
            </div>
          ) : (
            // Image preview when selected
            <div style={{ position: 'relative' }}>
              <div style={{
                width: '100%',
                height: '160px',
                border: '1px solid var(--color-border-secondary)',
                borderRadius: '8px',
                overflow: 'hidden',
                background: 'var(--color-bg-container)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center'
              }}>
                <img
                  src={toAppUrl(thumbnailPath) || undefined}
                  alt="Thumbnail preview"
                  style={{
                    maxWidth: '100%',
                    maxHeight: '100%',
                    objectFit: 'contain'
                  }}
                  onError={(e) => {
                    (e.target as HTMLImageElement).src = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
                  }}
                />
              </div>
              <div style={{
                marginTop: '8px',
                display: 'flex',
                justifyContent: 'space-between',
                alignItems: 'center'
              }}>
                <div style={{
                  color: 'var(--color-text-secondary)',
                  fontSize: '12px',
                  overflow: 'hidden',
                  textOverflow: 'ellipsis',
                  whiteSpace: 'nowrap',
                  flex: 1
                }}>
                  {thumbnailFileName}
                </div>
                <CompactButton
                  icon={<FolderOpenOutlined />}
                  onClick={handleBrowseThumbnail}
                  size="small"
                  style={{ marginLeft: '8px' }}
                >
                  Change
                </CompactButton>
              </div>
            </div>
          )}
        </Form.Item>
      </Form>

      <div className="slide-in-screen-footer">
        <Space>
          <CompactButton onClick={handleCancel} size="large">
            Cancel
          </CompactButton>
          <CompactPrimaryButton
            onClick={handleSubmit}
            loading={loading}
            size="large"
          >
            Save Classification
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

  const openClassificationScreen = (props: ClassificationScreenProps) => {
    // Create a wrapper that will receive the screenId
    let actualScreenId = '';

    const ContentWrapper = () => (
      <ClassificationScreenContent {...props} screenId={actualScreenId} />
    );

    actualScreenId = openScreen({
      title: 'Add Classification',
      width: '50%',
      content: <ContentWrapper />,
    });

    return actualScreenId;
  };

  return { openClassificationScreen };
}

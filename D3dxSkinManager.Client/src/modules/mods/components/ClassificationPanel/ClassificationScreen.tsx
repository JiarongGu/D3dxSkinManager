import { notification } from "../../../../shared/utils/notification";
import React, { useState, useEffect, useRef } from "react";
import { Form, Space, Row, Col, Select } from "antd";
import { FolderOpenOutlined } from "@ant-design/icons";
import { ClassificationNode } from "../../../../shared/types/classification.types";
import { useSlideInScreenContext } from "../../../../shared/context/SlideInScreenContext";
import { systemService } from "../../../../shared/services/systemService";
import { toAppUrl } from "../../../../shared/utils/imageUrlHelper";
import { classificationService } from "../../../../shared/services/classificationService";
import { useProfile } from "../../../../shared/context/ProfileContext";
import {
  CompactInput,
  CompactTextArea,
  CompactSelect,
  CompactButton,
  CompactPrimaryButton,
  CompactUpload,
} from "../../../../shared/components/compact";
import { useTranslation } from "react-i18next";
import { useDropZone } from "../../../../shared/hooks/useDropZone";
import "./ClassificationScreen.css";

const { Option } = Select;

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
    matchMode?: string;
    matchPattern?: string;
  }) => Promise<void>;
}

/**
 * Flatten tree to get all nodes for parent selection
 */
function flattenTree(nodes: ClassificationNode[]): ClassificationNode[] {
  const result: ClassificationNode[] = [];

  const traverse = (node: ClassificationNode) => {
    result.push(node);
    node.children.forEach((child) => traverse(child));
  };

  nodes.forEach((node) => traverse(node));
  return result;
}

/**
 * Content component for classification creation/editing
 */
export const ClassificationScreenContent: React.FC<
  ClassificationScreenProps & { screenId: string }
> = ({ parentId, tree, editNode, onSave, screenId }) => {
  const { t } = useTranslation();
  const [form] = Form.useForm();
  const [loading, setLoading] = useState(false);
  const [thumbnailPath, setThumbnailPath] = useState<string>();
  const [thumbnailFileName, setThumbnailFileName] = useState<string>();
  const [matchMode, setMatchMode] = useState<string>("wildcard");
  const { closeScreen } = useSlideInScreenContext();
  const { selectedProfileId } = useProfile();
  const dropZoneRef = useRef<HTMLDivElement>(null);

  // Create WinForms drop zone overlay that syncs with the upload area
  useDropZone({
    targetRef: dropZoneRef,
    enabled: !thumbnailPath, // Only enable when no thumbnail is set
    onDrop: (files) => {
      if (files.length === 0) return;

      const filePath = files[0]; // Take first file

      // Check if it's an image file
      const imageExtensions = [
        ".png",
        ".jpg",
        ".jpeg",
        ".gif",
        ".bmp",
        ".webp",
      ];
      const ext = filePath.toLowerCase().match(/\.[^.]+$/)?.[0];

      if (!ext || !imageExtensions.includes(ext)) {
        notification.warning(t("classification.screen.dropImageOnly"));
        return;
      }

      // Use the real file path directly
      setThumbnailPath(filePath);
      const fileName = filePath.split(/[\\/]/).pop() || filePath;
      setThumbnailFileName(fileName);
    },
  });

  // Handle file drops from CompactUpload component (browser-level drops)
  const handleDropThumbnail = async (file: File, filePath?: string) => {
    try {
      // Check if it's an image file
      const imageExtensions = [
        ".png",
        ".jpg",
        ".jpeg",
        ".gif",
        ".bmp",
        ".webp",
      ];
      const ext = file.name.toLowerCase().match(/\.[^.]+$/)?.[0];

      if (!ext || !imageExtensions.includes(ext)) {
        notification.warning(t("classification.screen.dropImageOnly"));
        return;
      }

      // If we received a real file path from electron/webkitGetAsEntry, use it
      if (filePath && filePath.length > 1) {
        setThumbnailPath(filePath);
        setThumbnailFileName(file.name);
        return;
      }

      // Fallback: Create object URL for preview
      const objectUrl = URL.createObjectURL(file);
      setThumbnailPath(objectUrl);
      setThumbnailFileName(file.name);
    } catch (error: unknown) {
      const errorMessage =
        error instanceof Error ? error.message : "Unknown error";
      notification.error(
        `${t("classification.screen.selectThumbnailFailed")}: ${errorMessage}`,
      );
      console.error("[ClassificationScreen] Failed to set thumbnail:", error);
    }
  };

  // Initialize form
  useEffect(() => {
    form.resetFields();

    if (editNode) {
      // Edit mode - populate with existing node data
      form.setFieldsValue({
        name: editNode.name,
        parentId: editNode.parentId || "",
        description: editNode.description,
        matchPattern: editNode.matchPattern || "",
      });
      setMatchMode(editNode.matchMode?.toLowerCase() || "wildcard");
      setThumbnailPath(editNode.thumbnail || undefined);
      if (editNode.thumbnail) {
        const fileName =
          editNode.thumbnail.split(/[\\/]/).pop() || editNode.thumbnail;
        setThumbnailFileName(fileName);
      }
    } else {
      // Create mode - clear and set parent if provided
      setThumbnailPath(undefined);
      setThumbnailFileName(undefined);
      setMatchMode("wildcard");
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
        description: values.description,
        matchMode: matchMode,
        matchPattern: values.matchPattern,
      });

      notification.success(t("classification.screen.saved"));
      closeScreen(screenId);
    } catch (error: any) {
      if (error.errorFields) {
        // Form validation error
        return;
      }
      console.error("Failed to save classification:", error);
      notification.error(t("classification.screen.saveFailed"));
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
        title: t("classification.screen.selectThumbnailTitle"),
        filters: [
          {
            name: t("classification.screen.imageFiles"),
            extensions: ["png", "jpg", "jpeg", "gif", "bmp", "webp"],
          },
        ],
        rememberPathKey: "classificationThumbnail",
      });

      if (result.success && result.filePath) {
        setThumbnailPath(result.filePath);
        const fileName =
          result.filePath.split(/[\\/]/).pop() || result.filePath;
        setThumbnailFileName(fileName);
      }
    } catch (error: unknown) {
      const errorMessage =
        error instanceof Error ? error.message : "Unknown error";
      notification.error(
        `${t("classification.screen.selectThumbnailFailed")}: ${errorMessage}`,
      );
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
          parentId: parentId,
        }}
      >
        {/* Compact row: Name and Parent side by side */}
        <Row gutter={16}>
          <Col span={12}>
            <Form.Item
              name="name"
              label={t("classification.screen.nameLabel")}
              rules={[
                {
                  required: true,
                  message: t("classification.screen.nameRequired"),
                },
                {
                  min: 1,
                  max: 100,
                  message: t("classification.screen.nameLength"),
                },
                {
                  validator: async (_: any, value: string) => {
                    if (!value || !selectedProfileId) return Promise.resolve();
                    // Check if classification name already exists in database (case-insensitive)
                    const exists = await classificationService.nameExists(
                      selectedProfileId,
                      value,
                      editNode?.id, // Exclude current node when editing
                    );
                    if (exists) {
                      return Promise.reject(
                        t("classification.screen.nameExists"),
                      );
                    }
                    return Promise.resolve();
                  },
                },
              ]}
            >
              <CompactInput
                placeholder={t("classification.screen.namePlaceholder")}
                autoFocus
              />
            </Form.Item>
          </Col>
          <Col span={12}>
            <Form.Item
              name="parentId"
              label={t("classification.screen.parentLabel")}
            >
              <CompactSelect
                placeholder={t("classification.screen.parentPlaceholder")}
                allowClear
                showSearch
                filterOption={(input, option) =>
                  (option?.label?.toString().toLowerCase() ?? "").includes(
                    input.toLowerCase(),
                  )
                }
                options={[
                  { value: "", label: t("classification.screen.rootNoParent") },
                  ...allNodes.map((node) => ({
                    value: node.id,
                    label: node.name,
                  })),
                ]}
              />
            </Form.Item>
          </Col>
        </Row>

        {/* Compact description - smaller textarea */}
        <Form.Item
          name="description"
          label={t("classification.screen.descriptionLabel")}
        >
          <CompactTextArea
            placeholder={t("classification.screen.descriptionPlaceholder")}
            rows={2}
            maxLength={500}
            showCount
          />
        </Form.Item>

        {/* Auto-detection pattern - single line with mode selector and pattern input */}
        <Form.Item
          label={t("classification.screen.autoDetection.label")}
          tooltip={t("classification.screen.autoDetection.tooltip")}
        >
          <Space.Compact style={{ width: '100%' }}>
            <CompactSelect
              value={matchMode}
              onChange={(value) => setMatchMode(value)}
              style={{ width: '140px' }}
            >
              <Option value="wildcard">
                {t("classification.screen.autoDetection.wildcard")}
              </Option>
              <Option value="regex">
                {t("classification.screen.autoDetection.regex")}
              </Option>
            </CompactSelect>
            <Form.Item
              name="matchPattern"
              noStyle
            >
              <CompactInput
                placeholder={matchMode === "wildcard" ? t("classification.screen.autoDetection.wildcardPlaceholder") : t("classification.screen.autoDetection.regexPlaceholder")}
              />
            </Form.Item>
          </Space.Compact>
        </Form.Item>

        {/* Thumbnail with drag-drop area or preview */}
        <Form.Item label={t("classification.screen.thumbnailLabel")}>
          {!thumbnailPath ? (
            // Drag-drop area when no image selected - with OS-level drop zone
            <div ref={dropZoneRef}>
              <CompactUpload
                onSelect={handleBrowseThumbnail}
                onDrop={handleDropThumbnail}
                title={t("classification.screen.thumbnailUploadTitle")}
                subtitle={t("classification.screen.thumbnailUploadSubtitle")}
              />
            </div>
          ) : (
            // Image preview when selected
            <div className="classification-screen-thumbnail-preview">
              <img
                src={toAppUrl(thumbnailPath) || undefined}
                alt={t("classification.screen.thumbnailPreview")}
                className="classification-screen-thumbnail-image"
                onError={(e) => {
                  (e.target as HTMLImageElement).src =
                    "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
                }}
              />
            </div>
          )}

          {/* Image info and actions - outside dropzone */}
          {thumbnailPath && (
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
                  {t("classification.screen.changeButton")}
                </CompactButton>
                <CompactButton
                  danger
                  onClick={() => {
                    setThumbnailPath(undefined);
                    setThumbnailFileName(undefined);
                  }}
                  size="small"
                >
                  {t("classification.screen.removeButton")}
                </CompactButton>
              </Space>
            </div>
          )}
        </Form.Item>
      </Form>

      <div className="slide-in-screen-footer">
        <Space>
          <CompactButton onClick={handleCancel} size="large">
            {t("classification.screen.cancelButton")}
          </CompactButton>
          <CompactPrimaryButton
            onClick={handleSubmit}
            loading={loading}
            size="large"
          >
            {t("classification.screen.saveButton")}
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
  const { openScreen } = useSlideInScreenContext();
  const { t } = useTranslation();

  const openClassificationScreen = (props: ClassificationScreenProps) => {
    // Create a wrapper that will receive the screenId
    let actualScreenId = "";

    const ContentWrapper = () => (
      <ClassificationScreenContent {...props} screenId={actualScreenId} />
    );

    const title = props.editNode
      ? t("classification.screen.title.edit")
      : t("classification.screen.title.add");

    actualScreenId = openScreen({
      title,
      width: "50%",
      content: <ContentWrapper />,
    });

    return actualScreenId;
  };

  return { openClassificationScreen };
}

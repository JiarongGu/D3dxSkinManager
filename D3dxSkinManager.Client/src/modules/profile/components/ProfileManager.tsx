import React, { useState, useEffect } from "react";
import {
  Flex,
  Space,
  Tag,
  Tooltip,
  Form,
  ColorPicker,
  Spin,
  Row,
  Col,
} from "antd";
import {
  PlusOutlined,
  EditOutlined,
  DeleteOutlined,
  SwapOutlined,
} from "@ant-design/icons";
import { StatusTag } from "../../../shared/components/common/StatusTag";
import classNames from "classnames";
import { useTranslation } from "react-i18next";
import {
  Profile,
  CreateProfileRequest,
  UpdateProfileRequest,
} from "../../../shared/types/profile.types";
import { useProfile } from "../../../shared/context/ProfileContext";
import { handleError } from "../../../shared/utils/errorHandler";
import { FormDialog, ConfirmDialog } from "../../../shared/components/dialogs";
import { toAppUrl } from "../../../shared/utils/imageUrlHelper";
import "./ProfileManager.css";
import { notification } from "../../../shared/utils/notification";
import { profileService, systemService } from "../../../shared/services/ipc";
import { CompactThumbnailUpload, CompactPrimaryButton, CompactIconButton, CompactInput, CompactTextArea } from "../../../shared/components/compact";

interface ProfileManagerProps {
  onProfileChanged?: () => void;
}

export const ProfileManager: React.FC<ProfileManagerProps> = ({
  onProfileChanged,
}) => {
  const { t } = useTranslation();
  const { selectedProfileId, actions } = useProfile();
  const [profiles, setProfiles] = useState<Profile[]>([]);
  const [activeProfileId, setActiveProfileId] = useState<string>("");
  const [loading, setLoading] = useState(false);
  const [editingProfile, setEditingProfile] = useState<Profile>();
  const [showCreateDialog, setShowCreateDialog] = useState(false);
  const [createForm] = Form.useForm();
  const [editForm] = Form.useForm();
  const [createThumbnailPath, setCreateThumbnailPath] = useState<string>();
  const [editThumbnailPath, setEditThumbnailPath] = useState<string>();
  const [editNewThumbnailPath, setEditNewThumbnailPath] = useState<string>(); // Separate state for new uploads
  const [editThumbnailRemoved, setEditThumbnailRemoved] = useState(false); // Track if user explicitly removed thumbnail
  const [deleteConfirmVisible, setDeleteConfirmVisible] = useState(false);
  const [profileToDelete, setProfileToDelete] = useState<string>();

  useEffect(() => {
    loadProfiles();
  }, []);

  const loadProfiles = async () => {
    try {
      setLoading(true);
      const result = await profileService.getAllProfiles();
      setProfiles(result.profiles);
      setActiveProfileId(result.activeProfileId);
    } catch (error: unknown) {
      handleError(error);
    } finally {
      setLoading(false);
    }
  };

  const handleCreate = async () => {
    try {
      const values = await createForm.validateFields();

      const request: CreateProfileRequest = {
        name: values.name,
        description: values.description,
        color: values.color?.toHexString?.() || values.color,
        gameName: values.gameName,
        thumbnailPath: createThumbnailPath,
      };

      await profileService.createProfile(request);
      notification.success(t("profiles.notifications.createSuccess"));

      setShowCreateDialog(false);
      createForm.resetFields();
      setCreateThumbnailPath(undefined);

      // Reload both local and global profile lists
      await loadProfiles();
      await actions.loadProfiles();

      if (onProfileChanged) {
        onProfileChanged();
      }
    } catch (error: unknown) {
      handleError(error);
    }
  };

  const handleEdit = async () => {
    if (!editingProfile) return;

    try {
      const values = await editForm.validateFields();

      const request: UpdateProfileRequest = {
        profileId: editingProfile.id,
        name: values.name,
        description: values.description,
        color: values.color?.toHexString?.() || values.color,
        gameName: values.gameName,
        // Send thumbnailPath based on user action:
        // - If user uploaded new thumbnail: send the new path
        // - If user explicitly removed thumbnail: send empty string to clear it
        // - Otherwise: send undefined to keep existing thumbnail
        thumbnailPath: editThumbnailRemoved ? "" : editNewThumbnailPath,
      };

      await profileService.updateProfile(request);

      notification.success(t("profiles.notifications.updateSuccess"));
      setEditingProfile(undefined);
      setEditThumbnailPath(undefined);
      setEditNewThumbnailPath(undefined);
      setEditThumbnailRemoved(false);
      await loadProfiles();

      // Reload profiles in the context to update ProfileSwitcher
      await actions.loadProfiles();

      // If the edited profile is the currently selected one, update it in context
      if (editingProfile.id === selectedProfileId) {
        const updatedProfile = await profileService.getProfileById(
          editingProfile.id,
        );
        if (updatedProfile) {
          actions.setSelectedProfile(updatedProfile);
        }
      }

      if (onProfileChanged) {
        onProfileChanged();
      }
    } catch (error: unknown) {
      handleError(error);
    }
  };

  const handleDeleteConfirm = async () => {
    if (!profileToDelete) return;

    try {
      await profileService.deleteProfile(profileToDelete);
      notification.success(t("profiles.notifications.deleteSuccess"));
      setDeleteConfirmVisible(false);
      setProfileToDelete(undefined);
      await loadProfiles();

      if (onProfileChanged) {
        onProfileChanged();
      }
    } catch (error: unknown) {
      handleError(error);
    }
  };

  const handleSwitch = async (profileId: string) => {
    try {
      // Use the context action to properly update global profile state
      await actions.selectProfile(profileId);
      notification.success(t("profiles.notifications.switchSuccess"));
      await loadProfiles();

      if (onProfileChanged) {
        onProfileChanged();
      }
    } catch (error: unknown) {
      handleError(error);
    }
  };

  const handleBrowseCreateThumbnail = async () => {
    try {
      const result = await systemService.openFileDialog({
        title: t("profiles.form.thumbnail.selectTitle"),
        filters: [
          {
            name: t("common.imageFiles"),
            extensions: ["png", "jpg", "jpeg", "gif", "bmp", "webp"],
          },
        ],
        rememberPathKey: "profile-thumbnail",
      });

      if (result.success && result.filePath) {
        setCreateThumbnailPath(result.filePath);
      }
    } catch (error: unknown) {
      handleError(error);
    }
  };

  const handleBrowseEditThumbnail = async () => {
    try {
      const result = await systemService.openFileDialog({
        title: t("profiles.form.thumbnail.selectTitle"),
        filters: [
          {
            name: t("common.imageFiles"),
            extensions: ["png", "jpg", "jpeg", "gif", "bmp", "webp"],
          },
        ],
        rememberPathKey: "profile-thumbnail",
      });

      if (result.success && result.filePath) {
        // For edit mode, update both display path and new upload path
        setEditThumbnailPath(result.filePath);
        setEditNewThumbnailPath(result.filePath);
        setEditThumbnailRemoved(false); // Clear removal flag when new thumbnail selected
      }
    } catch (error: unknown) {
      handleError(error);
    }
  };

  return (
    <>
      <div className="profile-manager-container">
        <Flex
          vertical
          className="profile-manager-vertical-space"
          gap="large"
        >
          <Spin spinning={loading}>
            <Flex vertical gap="middle">
              {profiles.map((profile) => (
                <Flex
                  key={profile.id}
                  justify="space-between"
                  align="center"
                  className={classNames("profile-manager-item", {
                    "profile-manager-item--active":
                      profile.id === activeProfileId,
                    "profile-manager-item--inactive":
                      profile.id !== activeProfileId,
                  })}
                  style={{
                    borderLeft: `4px solid ${profile.color || "var(--color-primary)"}`,
                  }}
                >
                  <Flex
                    align="flex-start"
                    gap="middle"
                    className="profile-manager-content"
                  >
                    {/* Thumbnail or Avatar - Show thumbnail if available */}
                    {profile.thumbnail ? (
                      <img
                        src={toAppUrl(profile.thumbnail) || undefined}
                        alt={profile.name}
                        className="profile-manager-thumbnail"
                        onError={(e) => {
                          // If thumbnail fails to load, hide it and show avatar instead
                          e.currentTarget.style.display = 'none';
                          const avatar = e.currentTarget.nextElementSibling as HTMLElement;
                          if (avatar) avatar.style.display = 'flex';
                        }}
                      />
                    ) : null}
                    {/* Avatar fallback - shown if no thumbnail or if thumbnail fails to load */}
                    <div
                      className="profile-manager-avatar"
                      style={{
                        backgroundColor: profile.color || "#1890ff",
                        display: profile.thumbnail ? 'none' : 'flex'
                      }}
                    >
                      {profile.name.charAt(0).toUpperCase()}
                    </div>
                    <Flex
                      vertical
                      gap="small"
                      className="profile-manager-content"
                    >
                      <Space>
                        <span className="profile-manager-name">
                          {profile.name}
                        </span>
                        {profile.id === activeProfileId && (
                          <StatusTag tone="success" label={t("profiles.badge.active")} />
                        )}
                        {profile.gameName && (
                          <Tag color="blue">{profile.gameName}</Tag>
                        )}
                      </Space>
                      {profile.description && (
                        <span className="profile-manager-description">
                          {profile.description}
                        </span>
                      )}
                    </Flex>
                  </Flex>
                  <Space>
                    {profile.id !== activeProfileId && (
                      <Tooltip title={t("profiles.tooltip.switch")}>
                        <CompactIconButton
                          icon={<SwapOutlined />}
                          onClick={() => handleSwitch(profile.id)}
                        />
                      </Tooltip>
                    )}
                    <Tooltip title={t("common.edit")}>
                      <CompactIconButton
                        icon={<EditOutlined />}
                        onClick={() => {
                          setEditingProfile(profile);
                          setEditThumbnailPath(profile.thumbnail);
                          setEditNewThumbnailPath(undefined); // Reset new upload
                          setEditThumbnailRemoved(false); // Reset removal flag
                          editForm.setFieldsValue({
                            name: profile.name,
                            description: profile.description,
                            color: profile.color,
                            gameName: profile.gameName,
                          });
                        }}
                      />
                    </Tooltip>
                    {profile.id !== activeProfileId && (
                      <Tooltip title={t("common.delete")}>
                        <CompactIconButton
                          tone="danger"
                          icon={<DeleteOutlined />}
                          onClick={() => {
                            setProfileToDelete(profile.id);
                            setDeleteConfirmVisible(true);
                          }}
                        />
                      </Tooltip>
                    )}
                  </Space>
                </Flex>
              ))}
            </Flex>
          </Spin>

          <CompactPrimaryButton
            icon={<PlusOutlined />}
            onClick={() => setShowCreateDialog(true)}
            block
          >
            {t("profiles.button.createNew")}
          </CompactPrimaryButton>
        </Flex>
      </div>

      {/* Create Profile Dialog */}
      <FormDialog
        visible={showCreateDialog}
        title={t("profiles.dialog.createTitle")}
        onCancel={() => {
          setShowCreateDialog(false);
          createForm.resetFields();
          setCreateThumbnailPath(undefined);
        }}
        onOk={handleCreate}
        okText={t("common.save")}
        cancelText={t("common.cancel")}
        width={560}
      >
        <Form form={createForm} layout="vertical">
          <Row gutter={12}>
            <Col span={16}>
              <Form.Item
                label={t("profiles.form.name.label")}
                name="name"
                rules={[
                  { required: true, message: t("profiles.form.name.required") },
                ]}
                style={{ marginBottom: 12 }}
              >
                <CompactInput placeholder={t("profiles.form.name.placeholder")} />
              </Form.Item>
            </Col>
            <Col span={8}>
              <Form.Item
                label={t("profiles.form.color.label")}
                name="color"
                style={{ marginBottom: 12 }}
              >
                <ColorPicker showText style={{ width: "100%" }} />
              </Form.Item>
            </Col>
          </Row>

          <Form.Item
            label={t("profiles.form.gameName.label")}
            name="gameName"
            style={{ marginBottom: 12 }}
          >
            <CompactInput placeholder={t("profiles.form.gameName.placeholder")} />
          </Form.Item>

          <Form.Item
            label={t("common.description")}
            name="description"
            style={{ marginBottom: 12 }}
          >
            <CompactTextArea
              rows={2}
              placeholder={t("profiles.form.description.placeholder")}
            />
          </Form.Item>

          <Form.Item
            label={t("profiles.form.thumbnail.label")}
            style={{ marginBottom: 0 }}
          >
            <CompactThumbnailUpload
              thumbnailUrl={createThumbnailPath ? toAppUrl(createThumbnailPath) || undefined : undefined}
              onSelect={handleBrowseCreateThumbnail}
              onRemove={() => setCreateThumbnailPath(undefined)}
              buttonText={t("profiles.form.thumbnail.upload")}
              alt={t("common.profileThumbnail")}
            />
          </Form.Item>
        </Form>
      </FormDialog>

      {/* Edit Profile Dialog */}
      <FormDialog
        visible={editingProfile !== undefined}
        title={t("profiles.dialog.editTitle")}
        onCancel={() => {
          setEditingProfile(undefined);
          setEditThumbnailPath(undefined);
        }}
        onOk={handleEdit}
        okText={t("common.save")}
        cancelText={t("common.cancel")}
        width={560}
      >
        <Form form={editForm} layout="vertical">
          <Row gutter={12}>
            <Col span={16}>
              <Form.Item
                label={t("profiles.form.name.label")}
                name="name"
                rules={[
                  { required: true, message: t("profiles.form.name.required") },
                ]}
                style={{ marginBottom: 12 }}
              >
                <CompactInput />
              </Form.Item>
            </Col>
            <Col span={8}>
              <Form.Item
                label={t("profiles.form.color.label")}
                name="color"
                style={{ marginBottom: 12 }}
              >
                <ColorPicker showText style={{ width: "100%" }} />
              </Form.Item>
            </Col>
          </Row>

          <Form.Item
            label={t("profiles.form.gameName.label")}
            name="gameName"
            style={{ marginBottom: 12 }}
          >
            <CompactInput />
          </Form.Item>

          <Form.Item
            label={t("common.description")}
            name="description"
            style={{ marginBottom: 12 }}
          >
            <CompactTextArea rows={2} />
          </Form.Item>

          <Form.Item
            label={t("profiles.form.thumbnail.label")}
            style={{ marginBottom: 0 }}
          >
            <CompactThumbnailUpload
              thumbnailUrl={editThumbnailPath ? toAppUrl(editThumbnailPath) || undefined : undefined}
              onSelect={handleBrowseEditThumbnail}
              onRemove={() => {
                setEditThumbnailPath(undefined);
                setEditNewThumbnailPath(undefined);
                setEditThumbnailRemoved(true); // Mark that user wants to remove thumbnail
              }}
              buttonText={t("profiles.form.thumbnail.change")}
              alt={t("common.profileThumbnail")}
            />
          </Form.Item>
        </Form>
      </FormDialog>

      {/* Delete Confirmation Dialog */}
      <ConfirmDialog
        visible={deleteConfirmVisible}
        title={t("profiles.delete.title")}
        content={t("profiles.delete.description")}
        okText={t("common.delete")}
        cancelText={t("common.cancel")}
        okType="danger"
        onOk={handleDeleteConfirm}
        onCancel={() => {
          setDeleteConfirmVisible(false);
          setProfileToDelete(undefined);
        }}
      />
    </>
  );
};

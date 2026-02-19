import React, { useMemo, useCallback, useState, useEffect } from "react";
import { Layout, message } from "antd";
import {
  FolderOutlined,
  AppstoreOutlined,
  EditOutlined,
  PlusOutlined,
} from "@ant-design/icons";
import type { DataNode } from "antd/es/tree";
import { ModInfo } from "../../../shared/types/mod.types";
import { ClassificationNode } from "../../../shared/types/classification.types";

import { ModPreviewPanel } from "./ModPreviewPanel";
import { ClassificationPanel } from "./ClassificationPanel";
import { ModListPanel } from "./ModListPanel";
import {
  ContextMenu,
  ContextMenuItem,
} from "../../../shared/components/common/ContextMenu";
import { DragDropZone } from "../../../shared/components/common/DragDropZone";
import { ModEditDialog } from "./ModEditDialog";
import { BatchEditDialog } from "./BatchEditDialog";
import { ImportTagSelectorDialog } from "./ImportTagSelectorDialog/ImportTagSelectorDialog";
import {
  AddModWindow,
  ImportTask,
  TaskStatus,
} from "./AddModWindow";
import { AddModUnit } from "./AddModUnit";
import { BatchEditUnit } from "./BatchEditUnit";
import { createDefaultFileRouter } from "../../../shared/utils/fileTypeRouter";
import { useModsContext } from "../context/ModsContext";
import { useProfile } from "../../../shared/context/ProfileContext";

const { Sider } = Layout;

export const ModHierarchicalView: React.FC = () => {
  const { state, actions } = useModsContext();
  const [unclassifiedCount, setUnclassifiedCount] = useState<number>(0);
  const [availableTagsForImport, setAvailableTagsForImport] = useState<
    string[]
  >([]);
  const { state: profileState } = useProfile();

  // Build classification tree from mods with search filtering
  const classificationTree = useMemo((): DataNode[] => {
    const objectMap = new Map<string, ModInfo[]>();

    state.mods.forEach((mod) => {
      const categoryName = mod.category || "Uncategorized";
      if (!objectMap.has(categoryName)) {
        objectMap.set(categoryName, []);
      }
      objectMap.get(categoryName)!.push(mod);
    });

    let sortedObjects = Array.from(objectMap.entries()).sort(([a], [b]) =>
      a.localeCompare(b),
    );

    // Apply classification search filter
    if (state.classificationSearch) {
      const searchLower = state.classificationSearch.toLowerCase();
      sortedObjects = sortedObjects.filter(([categoryName]) =>
        categoryName.toLowerCase().includes(searchLower),
      );
    }

    const totalMods = state.mods.length;
    const filteredObjectMods = sortedObjects.reduce(
      (sum, [, mods]) => sum + mods.length,
      0,
    );

    return [
      {
        key: "all",
        title: state.classificationSearch
          ? `All Mods [${filteredObjectMods}/${totalMods}]`
          : `All Mods (${totalMods})`,
        icon: <AppstoreOutlined />,
        children: sortedObjects.map(([categoryName, objectMods]) => ({
          key: categoryName,
          title: `${categoryName} (${objectMods.length})`,
          icon: <FolderOutlined />,
          isLeaf: true,
        })),
      },
    ];
  }, [state.mods, state.classificationSearch]);

  // Get mods for selected object/classification with mod search filtering
  const filteredMods = useMemo(() => {
    // If no classification and no object selected, return empty array
    if (!state.selectedClassification && !state.selectedObject) {
      return [];
    }

    // If a classification is selected, only use classification-filtered mods
    // Don't fall back to all mods while loading to prevent flash
    let result: ModInfo[];
    if (state.selectedClassification) {
      // When classification is selected, only show classificationFilteredMods
      // Return empty array if still loading (null) to avoid showing all mods flash
      result = state.classificationFilteredMods || [];
    } else {
      // No classification selected, use all mods (for object filtering)
      result = state.mods;
    }

    // Filter by selected object (only if no classification is selected)
    if (
      !state.selectedClassification &&
      state.selectedObject &&
      state.selectedObject !== "all"
    ) {
      result = result.filter((mod) => mod.category === state.selectedObject);
    }

    // Apply mod search filter
    if (state.searchQuery) {
      const searchLower = state.searchQuery.toLowerCase();
      result = result.filter(
        (mod) =>
          mod.name.toLowerCase().includes(searchLower) ||
          (mod.author && mod.author.toLowerCase().includes(searchLower)) ||
          (mod.tags &&
            mod.tags.some((tag) => tag.toLowerCase().includes(searchLower))),
      );
    }

    // Add "Unload" option at the beginning if object is selected and has loaded mod
    if (state.selectedObject && state.selectedObject !== "all") {
      const hasLoadedMod = result.some((mod) => mod.isLoaded);
      if (hasLoadedMod) {
        const unloadOption: ModInfo = {
          sha: "__UNLOAD__",
          name: "- [X] Unload This Object -",
          category: state.selectedObject,
          author: "",
          tags: [],
          grading: "",
          description: "",
          isLoaded: false,
          type: "special",
          isAvailable: true,
        };
        result = [unloadOption, ...result];
      }
    }

    return result;
  }, [
    state.mods,
    state.classificationFilteredMods,
    state.selectedObject,
    state.selectedClassification,
    state.searchQuery,
  ]);

  const handleTreeSelect = (selectedKeys: React.Key[]) => {
    const key = selectedKeys[0] as string;
    actions.setSelectedObject(key || "");
  };

  const handleModSelect = (mod: ModInfo) => {
    actions.selectMod(mod);
  };

  const handleClassificationSelect = useCallback(
    (node: ClassificationNode | null) => {
      actions.selectClassification(node);

      if (node) {
        // Check if this is the unclassified node
        if (node.id === "__unclassified__") {
          // Load unclassified mods
          actions.loadUnclassifiedMods();
        } else {
          // Load mods filtered by this classification node
          actions.loadModsByClassification(node.id);
        }
      } else {
        // Clear classification filter (show all mods)
        actions.clearClassificationFilter();
      }
    },
    [actions],
  );

  // Load unclassified count when component mounts or mods change
  useEffect(() => {
    const loadUnclassifiedCount = async () => {
      if (!profileState.selectedProfile?.id) {
        return;
      }
      const profileId = profileState.selectedProfile.id;
      try {
        const { modService } = await import("../services/modService");
        const count = await modService.getUnclassifiedCount(profileId);
        setUnclassifiedCount(count);
      } catch (error) {
        console.error("Failed to load unclassified count:", error);
      }
    };

    loadUnclassifiedCount();
  }, [state.mods.length, profileState.selectedProfile?.id]); // Reload when mods count changes

  // Load available tags for import workflow
  useEffect(() => {
    const loadAvailableTags = async () => {
      if (!profileState.selectedProfile?.id) {
        return;
      }
      const profileId = profileState.selectedProfile.id;
      try {
        const { modService } = await import("../services/modService");
        const tags = await modService.getTags(profileId);
        setAvailableTagsForImport(tags);
      } catch (error) {
        console.error("Failed to load available tags:", error);
      }
    };

    loadAvailableTags();
  }, [state.mods.length, profileState.selectedProfile?.id]); // Reload when mods count changes

  const handleUnclassifiedClick = () => {
    const unclassifiedNode: ClassificationNode = {
      id: "__unclassified__",
      name: "Unclassified",
      parentId: null,
      priority: 0,
      children: [],
      thumbnail: null,
      description: null,
    };
    handleClassificationSelect(unclassifiedNode);
  };

  // Context menu handlers for classification tree
  const handleModifyClassification = (category: string) => {
    // TODO: Open modify classification dialog
    message.info(`Modify classification: ${category}`);
  };

  const handleAddClassification = () => {
    // TODO: Open add classification dialog
    message.info("Add new classification");
  };

  const handleCopyObjectName = (category: string) => {
    navigator.clipboard.writeText(category);
    message.success(`Copied category: ${category}`);
  };

  // Get context menu items for tree node
  const getTreeNodeContextMenu = (
    nodeKey: string,
    nodeTitle: string,
  ): ContextMenuItem[] => {
    // Root "All Mods" node
    if (nodeKey === "all") {
      return [
        {
          key: "add-classification",
          label: "Add New Classification",
          icon: <PlusOutlined />,
          onClick: handleAddClassification,
        },
      ];
    }

    // Object/classification node
    return [
      {
        key: "modify",
        label: "Modify Classification",
        icon: <EditOutlined />,
        onClick: () => handleModifyClassification(nodeKey),
      },
      {
        key: "copy",
        label: "Copy Object Name",
        onClick: () => handleCopyObjectName(nodeKey),
      },
      { key: "divider-1", type: "divider" },
      {
        key: "add-classification",
        label: "Add New Classification",
        icon: <PlusOutlined />,
        onClick: handleAddClassification,
      },
    ];
  };

  // Custom title render with context menu
  const renderTreeTitle = (node: DataNode) => {
    const contextMenuItems = getTreeNodeContextMenu(
      node.key as string,
      node.title as string,
    );
    const titleText =
      typeof node.title === "string" ? node.title : String(node.title);

    return (
      <ContextMenu items={contextMenuItems}>
        <span style={{ display: "inline-block", width: "100%" }}>
          {titleText}
        </span>
      </ContextMenu>
    );
  };

  // Dialog handlers
  const handleOpenModEdit = (mod: ModInfo) => {
    actions.openEditDialog(mod);
  };

  const handleSaveModEdit = async (modData: Partial<ModInfo>) => {
    await actions.updateMod(state.modToEdit!.sha, modData);
    actions.closeEditDialog();
  };

  const handleOpenBatchEdit = (mods: ModInfo[]) => {
    actions.openBatchEditDialog(mods);
  };

  const handleSaveBatchEdit = async (
    modData: Partial<ModInfo>,
    fieldMask: string[],
  ) => {
    await actions.batchUpdateMetadata(
      state.selectedMods.map((m) => m.sha),
      modData,
      fieldMask,
    );
    actions.closeBatchEditDialog();
  };

  // File drop handlers for router
  const handleArchiveDrop = useCallback(
    (files: File[]) => {
      // Create import tasks from archive files
      const newTasks: ImportTask[] = files.map((file) => {
        const fileName = file.name;

        return {
          id: `TASK-${state.taskIdCounter + files.indexOf(file)}`,
          filePath: file.name, // In real implementation, would be full path
          fileName: fileName,
          fileType: "archive" as "archive" | "folder",
          status: "pending" as TaskStatus,
          progress: 0,
          modData: {
            name: fileName.replace(/\.(zip|rar|7z|tar|gz)$/i, ""),
            category: "",
            author: "",
            description: "",
            grading: "",
            tags: [],
          },
        };
      });

      actions.addImportTasks(newTasks);
      actions.openImportWindow();
    },
    [state.taskIdCounter, actions],
  );

  const handleImageDrop = useCallback(
    async (files: File[]) => {
      // Handle preview image imports
      if (!state.selectedMod) {
        message.warning("Please select a mod to add preview image");
        return;
      }

      if (files.length === 0) return;

      // For now, just show a message about preview image import
      // In a full implementation, we would upload the file to backend
      message.info(
        `Preview image import for mod: ${state.selectedMod.name} (${files.length} image(s))`,
      );

      // TODO: Upload image file to backend and call importPreviewImage
      // This would require file upload infrastructure
      console.log("Image drop:", files, "Target mod:", state.selectedMod.sha);
    },
    [state.selectedMod],
  );

  // Import task management handlers (fallback for non-routed drops)
  const handleFilesDrop = (files: File[]) => {
    // Create import tasks from dropped files
    const newTasks: ImportTask[] = files.map((file) => {
      const fileName = file.name;
      const fileType = fileName.match(/\.(zip|rar|7z|tar|gz)$/i)
        ? "archive"
        : "folder";

      return {
        id: `TASK-${state.taskIdCounter + files.indexOf(file)}`,
        filePath: file.name, // In real implementation, would be full path
        fileName: fileName,
        fileType: fileType,
        status: "pending" as TaskStatus,
        progress: 0,
        modData: {
          name: fileName.replace(/\.(zip|rar|7z|tar|gz)$/i, ""),
          category: "",
          author: "",
          description: "",
          grading: "",
          tags: [],
        },
      };
    });

    actions.addImportTasks(newTasks);
    actions.openImportWindow();
    message.success(`${files.length} file(s) added to import queue`);
  };

  // Create file router
  const fileRouter = useMemo(() => {
    return createDefaultFileRouter({
      onImageDrop: handleImageDrop,
      onArchiveDrop: handleArchiveDrop,
    });
  }, [handleImageDrop, handleArchiveDrop]);

  const handleEditImportTask = (task: ImportTask) => {
    actions.openAddModUnit(task);
  };

  const handleSaveImportTask = (taskId: string, modData: Partial<ModInfo>) => {
    const task = state.importTasks.find((t) => t.id === taskId);
    if (task) {
      actions.saveAddModUnit({
        ...task,
        modData: { ...task.modData, ...modData },
      });
    }
  };

  const handleRemoveImportTask = (taskId: string) => {
    actions.removeImportTask(taskId);
    message.info("Task removed from queue");
  };

  const handleBatchEditImportTasks = (taskIds: string[]) => {
    actions.openBatchEditUnit(taskIds);
  };

  const handleSaveBatchEditUnit = (
    taskIds: string[],
    modData: Partial<ModInfo>,
    fieldMask: string[],
  ) => {
    // Filter the modData to only include fields in the fieldMask
    const filteredData: Partial<ModInfo> = {};
    fieldMask.forEach((field) => {
      if (field in modData) {
        (filteredData as any)[field] = (modData as any)[field];
      }
    });
    actions.saveBatchEditUnit(filteredData);
  };

  const handleConfirmImport = async (tasks: ImportTask[]) => {
    await actions.importMods(tasks);
  };

  const handleOpenTagSelectorForImport = (tags: string[]) => {
    actions.openTagDialog(tags, "import");
  };

  const handleSaveTagsImport = (tags: string[]) => {
    actions.saveTagsForImport(tags);
  };

  return (
    <>
      <DragDropZone
        onFilesDrop={handleFilesDrop}
        accept={[
          ".zip",
          ".rar",
          ".7z",
          ".tar",
          ".gz",
          ".png",
          ".jpg",
          ".jpeg",
          ".gif",
          ".bmp",
          ".webp",
        ]}
        router={fileRouter}
        enableRouting={true}
      >
        <Layout
          style={{ height: "100%", background: "var(--color-bg-container)" }}
        >
          {/* Classification Tree - Left Panel */}
          <ClassificationPanel
            tree={state.classificationTree}
            loading={state.classificationLoading}
            selectedNode={state.selectedClassification}
            onSelect={handleClassificationSelect}
            searchQuery={state.classificationSearch}
            onSearchChange={actions.setClassificationSearch}
            expandedKeys={state.expandedKeys}
            onExpandedKeysChange={actions.setExpandedKeys}
            onRefreshTree={actions.loadClassificationTree}
            unclassifiedCount={unclassifiedCount}
            onUnclassifiedClick={handleUnclassifiedClick}
            isUnclassifiedSelected={
              state.selectedClassification?.id === "__unclassified__"
            }
          />

          {/* Mods Table - Center Panel */}
          <ModListPanel
            mods={filteredMods}
            loading={state.loading}
            selectedMod={state.selectedMod}
            searchQuery={state.searchQuery}
            onSearchChange={actions.setSearchQuery}
            onLoad={actions.loadModInGame}
            onUnload={actions.unloadModFromGame}
            onDelete={actions.deleteMod}
            onEdit={handleOpenModEdit}
            onRowClick={handleModSelect}
            selectedClassification={state.selectedClassification}
            selectedObject={state.selectedObject}
          />

          {/* Preview Panel - Right Panel */}
          <Sider
            width={500}
            style={{
              background: "var(--color-bg-spotlight)",
              borderLeft: "1px solid var(--color-border-secondary)",
              height: "100%",
              overflow: "auto",
            }}
          >
            <ModPreviewPanel mod={state.selectedMod} />
          </Sider>
        </Layout>
      </DragDropZone>

      {/* Dialogs */}
      <ModEditDialog
        visible={state.editDialogVisible}
        mod={state.modToEdit}
        onSave={handleSaveModEdit}
        onCancel={actions.closeEditDialog}
      />

      <BatchEditDialog
        visible={state.batchEditDialogVisible}
        selectedMods={state.selectedMods}
        onSave={handleSaveBatchEdit}
        onCancel={actions.closeBatchEditDialog}
      />

      {/* Import Tag Selector Dialog - Used for Import Workflow Only */}
      <ImportTagSelectorDialog
        visible={state.tagDialogVisible}
        availableTags={availableTagsForImport}
        selectedTags={state.currentTags}
        onConfirm={handleSaveTagsImport}
        onCancel={actions.closeTagDialog}
      />

      {/* Import Window */}
      <AddModWindow
        visible={state.importWindowVisible}
        tasks={state.importTasks}
        onConfirm={handleConfirmImport}
        onCancel={actions.closeImportWindow}
        onEditTask={handleEditImportTask}
        onRemoveTask={handleRemoveImportTask}
        onBatchEdit={handleBatchEditImportTasks}
        processing={state.importProcessing}
      />

      {/* Import Task Edit Dialog */}
      <AddModUnit
        visible={state.addModUnitVisible}
        task={state.currentEditTask}
        onSave={handleSaveImportTask}
        onCancel={actions.closeAddModUnit}
        onOpenTagSelector={handleOpenTagSelectorForImport}
      />

      {/* Import Task Batch Edit Dialog */}
      <BatchEditUnit
        visible={state.batchEditUnitVisible}
        selectedTasks={state.importTasks.filter((t) =>
          state.selectedTaskIds.includes(t.id),
        )}
        onSave={handleSaveBatchEditUnit}
        onCancel={actions.closeBatchEditUnit}
        onOpenTagSelector={handleOpenTagSelectorForImport}
      />
    </>
  );
};
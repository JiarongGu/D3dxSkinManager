import React, { createContext, useCallback, useContext, useState } from 'react';
import type { ModInfo } from '../../../../../shared/types/mod.types';
import type { CategoryInfo } from '../../../../../shared/types/category.types';
import type {
  PackageAnalysis,
  ExportResult,
  ImportResult,
  PackageProgress,
} from '../../../../../shared/types/modPackage.types';
import { api } from '../../../../../shared/services/ipc';
import { Module, ToolsEventType } from '../../../../../shared/services/eventBus';
import { useEventSubscription } from '../../../../../shared/hooks/useEventSubscription';
import { useProfile } from '../../../../../shared/context/ProfileContext';
import logger from '../../../../../shared/utils/logger';
import { handleError } from '../../../../../shared/utils/errorHandler';

export type OperationStatus = 'idle' | 'running' | 'done';

export interface ExportOptions {
  packageName: string;
  packageDescription: string;
  includeArchives: boolean;
  includePreviews: boolean;
}

interface ModPackageContextState {
  // Export state
  mods: ModInfo[];
  categories: CategoryInfo[];
  selectedModIds: Set<string>;
  setSelectedModIds: React.Dispatch<React.SetStateAction<Set<string>>>;
  exportOpts: ExportOptions;
  setExportOpts: React.Dispatch<React.SetStateAction<ExportOptions>>;
  exportResult: ExportResult | undefined;
  exportStatus: OperationStatus;

  // Import state
  packagePath: string;
  setPackagePath: (path: string) => void;
  analysis: PackageAnalysis | undefined;
  setAnalysis: (analysis: PackageAnalysis | undefined) => void;
  selectedImportIds: Set<string>;
  setSelectedImportIds: React.Dispatch<React.SetStateAction<Set<string>>>;
  importResult: ImportResult | undefined;
  importStatus: OperationStatus;

  // Shared
  loading: boolean;
  setLoading: (loading: boolean) => void;
  progress: PackageProgress | undefined;

  // Actions
  loadModsAndCategories: () => Promise<void>;
  startExport: () => Promise<void>;
  startImport: (options: { updateExisting: boolean; importPreviews: boolean; createCategories: boolean }) => Promise<void>;
  resetExport: () => void;
  resetImport: () => void;
}

const ModPackageContext = createContext<ModPackageContextState | undefined>(undefined);

export const useModPackage = (): ModPackageContextState => {
  const context = useContext(ModPackageContext);
  if (!context) throw new Error('useModPackage must be used within ModPackageProvider');
  return context;
};

const defaultExportOpts: ExportOptions = {
  packageName: '',
  packageDescription: '',
  includeArchives: true,
  includePreviews: true,
};

export const ModPackageProvider: React.FC<{ children: React.ReactNode; initialCategoryId?: string }> = ({ children, initialCategoryId }) => {
  const { selectedProfileId } = useProfile();

  // Export state
  const [mods, setMods] = useState<ModInfo[]>([]);
  const [categories, setCategories] = useState<CategoryInfo[]>([]);
  const [selectedModIds, setSelectedModIds] = useState<Set<string>>(new Set());
  const [exportOpts, setExportOpts] = useState<ExportOptions>(defaultExportOpts);
  const [exportResult, setExportResult] = useState<ExportResult>();
  const [exportStatus, setExportStatus] = useState<OperationStatus>('idle');

  // Import state
  const [packagePath, setPackagePath] = useState('');
  const [analysis, setAnalysis] = useState<PackageAnalysis>();
  const [selectedImportIds, setSelectedImportIds] = useState<Set<string>>(new Set());
  const [importResult, setImportResult] = useState<ImportResult>();
  const [importStatus, setImportStatus] = useState<OperationStatus>('idle');

  // Shared
  const [loading, setLoading] = useState(false);
  const [progress, setProgress] = useState<PackageProgress>();

  useEventSubscription(
    Module.TOOL,
    ToolsEventType.MOD_PACKAGE_PROGRESS,
    (payload) => { setProgress(payload); },
  );

  const loadModsAndCategories = useCallback(async () => {
    if (!selectedProfileId) return;
    try {
      setLoading(true);
      const [modsData, categoriesData] = await Promise.all([
        api.mod.getAllMods(selectedProfileId),
        api.category.getCategoryTree(selectedProfileId),
      ]);
      setMods(modsData);
      setCategories(categoriesData);

      // Auto-select mods from the initial category (including child categories)
      if (initialCategoryId) {
        const collectCategoryIds = (nodes: CategoryInfo[], targetId: string): Set<string> => {
          const ids = new Set<string>();
          const findAndCollect = (nodes: CategoryInfo[], found: boolean): boolean => {
            for (const node of nodes) {
              if (node.id === targetId || found) {
                ids.add(node.id);
                // Collect all children recursively
                const collectAll = (children: CategoryInfo[]) => {
                  for (const child of children) {
                    ids.add(child.id);
                    collectAll(child.children);
                  }
                };
                if (node.id === targetId) {
                  collectAll(node.children);
                  return true;
                }
              }
              if (findAndCollect(node.children, false)) return true;
            }
            return false;
          };
          findAndCollect(nodes, false);
          return ids;
        };

        const categoryIds = collectCategoryIds(categoriesData, initialCategoryId);
        const matchingModIds = new Set(
          modsData
            .filter(mod => mod.category && categoryIds.has(mod.category))
            .map(mod => mod.id)
        );
        if (matchingModIds.size > 0) {
          setSelectedModIds(matchingModIds);
        }
      }
    } catch (error) {
      logger.error('[ModPackage] Failed to load mods/categories', error);
    } finally {
      setLoading(false);
    }
  }, [selectedProfileId, initialCategoryId]);

  const startExport = useCallback(async () => {
    if (!selectedProfileId || !exportOpts.packageName) return;

    setExportResult(undefined);
    const dialogResult = await api.system.openFolderDialog({
      title: 'Select output folder',
      rememberPathKey: 'mod_export',
    });
    if (!dialogResult.success || !dialogResult.filePath) return;

    try {
      setExportStatus('running');
      setProgress(undefined);

      const result = await api.tool.exportModPackage(selectedProfileId, {
        packageName: exportOpts.packageName,
        packageDescription: exportOpts.packageDescription,
        outputPath: dialogResult.filePath,
        modIds: Array.from(selectedModIds),
        includeArchives: exportOpts.includeArchives,
        includePreviews: exportOpts.includePreviews,
      });

      setExportResult(result);
      setExportStatus('done');
    } catch (error: unknown) {
      handleError(error);
      setExportStatus('idle');
    }
  }, [selectedProfileId, exportOpts, selectedModIds]);

  const startImport = useCallback(async (options: { updateExisting: boolean; importPreviews: boolean; createCategories: boolean }) => {
    if (!selectedProfileId || selectedImportIds.size === 0) return;
    try {
      setImportStatus('running');
      setProgress(undefined);

      const result = await api.tool.importModPackage(selectedProfileId, {
        packagePath,
        selectedModIds: Array.from(selectedImportIds),
        updateExisting: options.updateExisting,
        importPreviews: options.importPreviews,
        createMissingCategories: options.createCategories,
      });

      setImportResult(result);
      setImportStatus('done');
    } catch (error: unknown) {
      handleError(error);
      setImportStatus('idle');
    }
  }, [selectedProfileId, selectedImportIds, packagePath]);

  const resetExport = useCallback(() => {
    setSelectedModIds(new Set());
    setExportOpts(defaultExportOpts);
    setExportResult(undefined);
    setExportStatus('idle');
    setProgress(undefined);
  }, []);

  const resetImport = useCallback(() => {
    setPackagePath('');
    setAnalysis(undefined);
    setSelectedImportIds(new Set());
    setImportResult(undefined);
    setImportStatus('idle');
    setProgress(undefined);
  }, []);

  const value: ModPackageContextState = {
    mods, categories,
    selectedModIds, setSelectedModIds,
    exportOpts, setExportOpts,
    exportResult, exportStatus,
    packagePath, setPackagePath,
    analysis, setAnalysis,
    selectedImportIds, setSelectedImportIds,
    importResult, importStatus,
    loading, setLoading,
    progress,
    loadModsAndCategories,
    startExport, startImport,
    resetExport, resetImport,
  };

  return (
    <ModPackageContext.Provider value={value}>
      {children}
    </ModPackageContext.Provider>
  );
};

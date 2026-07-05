import React, { useState, useCallback, useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { useSlideInScreen } from '../../../../shared/hooks/useSlideInScreen';
import { useProfile } from '../../../../shared/context/ProfileContext';
import { api } from '../../../../shared/services/ipc';
import { eventBus, Module, ToolsEventType } from '../../../../shared/services/eventBus';
import { handleError } from '../../../../shared/utils/errorHandler';
import { useStableRef } from '../../../../shared/hooks/useStableRef';
import { navigateToModSearch } from '../../../../shared/hooks/useAppNavigation';
import { notification } from '../../../../shared/utils/notification';
import { ConfirmDialog } from '../../../../shared/components/dialogs/ConfirmDialog';
import type { FullAnalysisReport, AnalysisProgress, AnalysisSessionSummary, ModAnalysisResult } from '../../../../shared/types/analysis.types';
import type { CategoryInfo } from '../../../../shared/types/category.types';
import type { ModFixTool as FixToolInfo } from '../../../../shared/types/modFix.types';
import { useAnalyzerUiStore } from '../../store/analyzerUiStore';
import { ScanView } from './components/ScanView';
import { FindingsView } from './components/FindingsView';
import { HistoryView } from './components/HistoryView';
import './ModAnalyzerTool.css';

interface ModAnalyzerToolProps {
  visible: boolean;
  onClose: () => void;
  initialCategoryId?: string;
}

export const ModAnalyzerTool: React.FC<ModAnalyzerToolProps> = ({ visible, onClose, initialCategoryId }) => {
  const { t } = useTranslation();
  const content = <ModAnalyzerToolInner initialCategoryId={initialCategoryId} onClose={onClose} />;

  useSlideInScreen({
    visible,
    title: t('tools.modAnalyzer.title'),
    content,
    width: '85%',
    onClose,
  });

  return null;
};

type ViewMode = 'scan' | 'findings' | 'history';

function findCategoryName(nodes: CategoryInfo[], id: string): string | undefined {
  for (const n of nodes) {
    if (n.id === id) return n.name;
    const found = findCategoryName(n.children, id);
    if (found) return found;
  }
  return undefined;
}

/** The analyzer content without the slide-in chrome — rendered directly in the pop-out window
 * (analyzer.tsx). onClose is a no-op there (the OS window has its own close). */
export const ModAnalyzerToolInner: React.FC<{ initialCategoryId?: string; onClose: () => void; inWindow?: boolean }> = ({ initialCategoryId, onClose, inWindow }) => {
  const { selectedProfileId } = useProfile();
  const { t } = useTranslation();

  const [viewMode, setViewMode] = useState<ViewMode>('scan');
  const [report, setReport] = useState<FullAnalysisReport>();
  const [scanning, setScanning] = useState(false);
  const [progress, setProgress] = useState<AnalysisProgress>();
  const [categories, setCategories] = useState<CategoryInfo[]>([]);
  const [selectedCategoryId, setSelectedCategoryId] = useState<string | undefined>(initialCategoryId);
  const [sessions, setSessions] = useState<AnalysisSessionSummary[]>([]);
  const [initialFeed, setInitialFeed] = useState<Array<{ name: string; status: string }>>();
  const [cancelling, setCancelling] = useState(false);
  const [deletingModId, setDeletingModId] = useState<string>();

  // ===== Persistent UI state (analyzerUiStore) =====
  // The tool unmounts on close (e.g. after "locate in mod list"). The store remembers the last
  // viewed session + findings filter/search. We do NOT auto-jump into stale findings on open (that
  // was jarring — you'd land in old results instead of the scan landing and couldn't tell it was
  // stale). Instead the scan landing offers an explicit "View last results" button, so returning to
  // the analyzed result after fixing / locating a mod is one obvious click.
  const analyzerUi = useAnalyzerUiStore();
  const [lastSessionId, setLastSessionId] = useState<string>();
  const restoredRef = useRef(false);
  useEffect(() => {
    if (!selectedProfileId || restoredRef.current) return;
    restoredRef.current = true;
    analyzerUi.ensureProfile(selectedProfileId);
    // A category open (context menu) starts a fresh scan — ignore the remembered session.
    if (initialCategoryId) return;
    setLastSessionId(useAnalyzerUiStore.getState().sessionId);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedProfileId]);

  // Load a remembered session's report on demand (the "View last results" button).
  const viewLastResults = useCallback(async () => {
    if (!selectedProfileId || !lastSessionId) return;
    try {
      const r = await api.tool.getAnalysisReport(selectedProfileId, lastSessionId);
      setReport(r);
      setViewMode('findings');
    } catch {
      setLastSessionId(undefined); // session gone — drop the affordance
      notification.info(t('tools.modAnalyzer.lastResultGone'));
    }
  }, [selectedProfileId, lastSessionId, t]);

  // Keep the store current so the NEXT open can offer "View last results".
  useEffect(() => { analyzerUi.setViewMode(viewMode); /* eslint-disable-line react-hooks/exhaustive-deps */ }, [viewMode]);
  useEffect(() => {
    analyzerUi.setSession(report?.sessionId);
    if (report?.sessionId) setLastSessionId(report.sessionId);
    /* eslint-disable-line react-hooks/exhaustive-deps */
  }, [report?.sessionId]);

  // ===== Fix tools (run a fix directly from a finding row — the analyzer stays open) =====
  const [fixTools, setFixTools] = useState<FixToolInfo[]>([]);
  useEffect(() => {
    if (!selectedProfileId) return;
    api.tool.getFixTools(selectedProfileId).then(setFixTools).catch(() => setFixTools([]));
  }, [selectedProfileId]);

  const runFix = useCallback(async (toolName: string, entryPath: string, recompress: boolean, modId: string) => {
    if (!selectedProfileId) return;
    try {
      await api.tool.runModFix(selectedProfileId, { scriptPath: entryPath, modIds: [modId], recompress });
      notification.info(t('mods.notifications.fixStarted', { name: toolName }));
    } catch (error: unknown) { handleError(error); }
  }, [selectedProfileId, t]);

  // Load categories
  useEffect(() => {
    if (!selectedProfileId) return;
    api.category.getCategoryTree(selectedProfileId).then(setCategories).catch(() => {});
  }, [selectedProfileId]);

  // Load history
  const loadHistory = useCallback(async () => {
    if (!selectedProfileId) return;
    try {
      const h = await api.tool.getAnalysisHistory(selectedProfileId);
      setSessions(h);
    } catch { /* ignore */ }
  }, [selectedProfileId]);

  useEffect(() => { void loadHistory(); }, [loadHistory]);

  // Stable refs to avoid re-subscribing on every state change
  const scanningRef = useStableRef(scanning);
  const viewModeRef = useStableRef(viewMode);
  const historyRefreshedRef = useRef(false);

  // Subscribe to events — detect in-progress scans even if we didn't start them
  // Shared logic: resume a running session (load partial results + switch to scan view)
  const resumeRunningSession = useCallback(async (sessionId: string) => {
    if (!selectedProfileId) return;
    try {
      const r = await api.tool.getAnalysisReport(selectedProfileId, sessionId);
      setInitialFeed(r.results.map(m => ({ name: m.modName, status: m.healthStatus })));
      setProgress({
        sessionId,
        stage: r.status === 'paused' ? 'paused' : 'analyzing',
        current: r.analyzedCount,
        total: r.totalMods,
        currentModName: '',
        status: r.status === 'paused' ? 'paused' : 'running',
        healthyCount: r.healthyCount,
        warningCount: r.warningCount,
        errorCount: r.errorCount,
      });
    } catch { /* ignore — live events will take over */ }
    setScanning(true);
    setViewMode('scan');
  }, [selectedProfileId]);

  useEffect(() => {
    const unsubProgress = eventBus.subscribe(Module.TOOL, ToolsEventType.MOD_ANALYSIS_PROGRESS, (e) => {
      const payload = e.payload;
      if (!payload) return;
      setProgress(payload);
      if (payload.status === 'running' && !scanningRef.current) {
        void resumeRunningSession(payload.sessionId);
      }
      if (payload.status === 'running' && !historyRefreshedRef.current) {
        historyRefreshedRef.current = true;
        void loadHistory();
      }
      setSessions(prev => prev.map(s =>
        s.id === payload.sessionId
          ? { ...s, analyzedCount: payload.current, healthyCount: payload.healthyCount, warningCount: payload.warningCount, errorCount: payload.errorCount }
          : s
      ));
    });
    const unsubComplete = eventBus.subscribe(Module.TOOL, ToolsEventType.MOD_ANALYSIS_COMPLETE, (e) => {
      if (e.payload?.status === 'running') return;

      setReport(e.payload);
      setScanning(false);
      setProgress(undefined);
      setCancelling(false);
      if (viewModeRef.current === 'scan') {
        setViewMode('findings');
      }
      void loadHistory();
    });
    return () => { unsubProgress(); unsubComplete(); };
  }, [loadHistory, scanningRef, viewModeRef, resumeRunningSession]);

  const doStartScan = useCallback(async (categoryId?: string) => {
    if (!selectedProfileId || scanning) return; // Single scan guard
    try {
      setScanning(true);
      setReport(undefined);
      setProgress(undefined);
      setInitialFeed(undefined);
      setViewMode('scan');
      historyRefreshedRef.current = false;
      await api.tool.startAnalysis(selectedProfileId, categoryId);
    } catch (error: unknown) {
      handleError(error);
      setScanning(false);
    }
  }, [selectedProfileId, scanning, categories, t]);

  // Auto-start scan when opened from context menu with a category
  // Wait for categories to load so the task label shows the category name
  useEffect(() => {
    if (initialCategoryId && selectedProfileId && !scanning && categories.length > 0) {
      setSelectedCategoryId(initialCategoryId);
      void doStartScan(initialCategoryId);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [initialCategoryId, categories.length]);

  const startScan = useCallback(() => { void doStartScan(selectedCategoryId); }, [doStartScan, selectedCategoryId]);

  const pauseScan = useCallback(async () => {
    if (!selectedProfileId) return;
    try { await api.tool.pauseAnalysis(selectedProfileId); }
    catch (error: unknown) { handleError(error); }
    // Don't clear scanning state — progress event with 'paused' status will update UI
  }, [selectedProfileId]);

  const resumeScan = useCallback(async () => {
    if (!selectedProfileId) return;
    // Optimistically switch to running state so UI updates immediately
    setProgress(prev => prev ? { ...prev, status: 'running', stage: 'analyzing' } : prev);
    try { await api.tool.resumeAnalysis(selectedProfileId, progress?.sessionId); }
    catch (error: unknown) { handleError(error); }
  }, [selectedProfileId, progress?.sessionId]);

  const cancelScan = useCallback(async () => {
    if (!selectedProfileId) return;
    setCancelling(true);
    try { await api.tool.cancelAnalysis(selectedProfileId); }
    catch (error: unknown) { handleError(error); setCancelling(false); }
    // COMPLETE event will fire with cancelled report → switches to findings and clears cancelling
  }, [selectedProfileId]);

  const viewSession = useCallback(async (sessionId: string, sessionStatus?: string) => {
    if (!selectedProfileId) return;
    // Running/paused → processing screen
    if (sessionStatus === 'running' || sessionStatus === 'paused') {
      void resumeRunningSession(sessionId);
      return;
    }
    // Completed/cancelled → findings screen
    try {
      const r = await api.tool.getAnalysisReport(selectedProfileId, sessionId);
      setReport(r);
      setViewMode('findings');
    } catch (error: unknown) { handleError(error); }
  }, [selectedProfileId, resumeRunningSession]);

  const deleteSession = useCallback(async (sessionId: string) => {
    if (!selectedProfileId) return;
    try {
      await api.tool.deleteAnalysisSession(selectedProfileId, sessionId);
      void loadHistory();
      if (report?.sessionId === sessionId) {
        setReport(undefined);
        setViewMode('scan');
      }
    } catch (error: unknown) { handleError(error); }
  }, [selectedProfileId, loadHistory, report]);

  const clearAll = useCallback(async () => {
    if (!selectedProfileId) return;
    try {
      await api.tool.clearAllAnalysis(selectedProfileId);
      setSessions([]);
      setReport(undefined);
      setViewMode('scan');
    } catch (error: unknown) { handleError(error); }
  }, [selectedProfileId]);

  const editModName = useCallback(async (modId: string, newName: string) => {
    if (!selectedProfileId) return;
    try {
      await api.mod.updateMetadata(selectedProfileId, modId, { name: newName });
      // Optimistically update mod name in report
      setReport(prev => {
        if (!prev) return prev;
        const updateName = <T extends { modId: string; modName: string }>(m: T): T =>
          m.modId === modId ? { ...m, modName: newName } : m;
        return {
          ...prev,
          results: prev.results.map(updateName),
          duplicateGroups: prev.duplicateGroups.map(g => ({ ...g, mods: g.mods.map(updateName) })),
          conflicts: prev.conflicts.map(c => ({ ...c, mods: c.mods.map(updateName) })),
        };
      });
    } catch (error: unknown) { handleError(error); }
  }, [selectedProfileId]);

  // Remove a set of mod ids from the report optimistically (single delete + group dedup share this).
  const removeFromReport = useCallback((modIds: string[]) => {
    const idSet = new Set(modIds);
    setReport(prev => {
      if (!prev) return prev;
      const newResults = prev.results.filter(r => !idSet.has(r.modId));
      const newGroups = prev.duplicateGroups
        .map(g => ({ ...g, mods: g.mods.filter(m => !idSet.has(m.modId)) }))
        .filter(g => g.mods.length > 1); // Dissolve single-mod groups
      const newConflicts = prev.conflicts
        .map(c => ({ ...c, mods: c.mods.filter(m => !idSet.has(m.modId)) }))
        .filter(c => c.mods.length > 1);
      return {
        ...prev,
        results: newResults,
        analyzedCount: newResults.length,
        totalMods: Math.max(prev.totalMods - modIds.length, newResults.length),
        duplicateGroups: newGroups,
        identicalCount: newGroups.filter(g => g.type === 'identical').length,
        textureVariantCount: newGroups.filter(g => g.type === 'textureVariant').length,
        conflicts: newConflicts,
        conflictCount: newConflicts.length,
        affectedModCount: new Set(newConflicts.flatMap(c => c.mods.map(m => m.modId))).size,
        healthyCount: newResults.filter(r => r.healthStatus === 'healthy').length,
        warningCount: newResults.filter(r => r.healthStatus === 'warning').length,
        errorCount: newResults.filter(r => r.healthStatus === 'error').length,
      };
    });
  }, []);

  // Dedup-assist: "keep this one, delete the rest" — staged into a ConfirmDialog, then batch-deleted
  // in the background (ONE cancellable process in the Activity panel).
  const [resolvingGroup, setResolvingGroup] = useState<{ keep: ModAnalysisResult; remove: ModAnalysisResult[] }>();

  const startResolveGroup = useCallback((keep: ModAnalysisResult, groupMods: ModAnalysisResult[]) => {
    const remove = groupMods.filter(m => m.modId !== keep.modId);
    if (remove.length > 0) setResolvingGroup({ keep, remove });
  }, []);

  const confirmResolveGroup = useCallback(async () => {
    const group = resolvingGroup;
    if (!group || !selectedProfileId) return;
    try {
      const ids = group.remove.map(m => m.modId);
      await api.mod.batchDeleteMods(selectedProfileId, ids);
      removeFromReport(ids);
      notification.info(t('tools.modAnalyzer.dedupStarted', { count: ids.length }));
    } catch (error: unknown) { handleError(error); }
    finally { setResolvingGroup(undefined); }
  }, [resolvingGroup, selectedProfileId, removeFromReport, t]);

  // One-click repair of unbalanced if/endif (analyzer finding) — requires the mod's cache.
  const [repairingModId, setRepairingModId] = useState<string>();
  const repairIni = useCallback(async (modId: string) => {
    if (!selectedProfileId || repairingModId) return;
    setRepairingModId(modId);
    try {
      const r = await api.mod.repairIniBalance(selectedProfileId, modId);
      if (r.filesChanged === 0) notification.info(t('tools.modAnalyzer.repairNothing'));
      else notification.success(t('tools.modAnalyzer.repairDone', { files: r.filesChanged, added: r.endifsAdded, removed: r.straysCommented }));
    } catch (error: unknown) { handleError(error); }
    finally { setRepairingModId(undefined); }
  }, [selectedProfileId, repairingModId, t]);

  const deleteDuplicateMod = useCallback(async (modId: string) => {
    if (!selectedProfileId || deletingModId) return;
    setDeletingModId(modId);
    try {
      await api.mod.deleteMod(selectedProfileId, modId);
      // Optimistically remove mod from report and update all counts
      setReport(prev => {
        if (!prev) return prev;
        const newResults = prev.results.filter(r => r.modId !== modId);
        const newGroups = prev.duplicateGroups
          .map(g => ({ ...g, mods: g.mods.filter(m => m.modId !== modId) }))
          .filter(g => g.mods.length > 1); // Dissolve single-mod groups
        const newConflicts = prev.conflicts
          .map(c => ({ ...c, mods: c.mods.filter(m => m.modId !== modId) }))
          .filter(c => c.mods.length > 1);
        return {
          ...prev,
          totalMods: Math.max(prev.totalMods - 1, 0),
          analyzedCount: newResults.length,
          results: newResults,
          duplicateGroups: newGroups,
          identicalCount: newGroups.filter(g => g.type === 'identical').length,
          textureVariantCount: newGroups.filter(g => g.type === 'textureVariant').length,
          conflicts: newConflicts,
          conflictCount: newConflicts.length,
          affectedModCount: new Set(newConflicts.flatMap(c => c.mods.map(m => m.modId))).size,
          healthyCount: newResults.filter(r => r.healthStatus === 'healthy').length,
          warningCount: newResults.filter(r => r.healthStatus === 'warning').length,
          errorCount: newResults.filter(r => r.healthStatus === 'error').length,
        };
      });
    } catch (error: unknown) { handleError(error); }
    finally { setDeletingModId(undefined); }
  }, [selectedProfileId, deletingModId, t]);

  const locateMods = useCallback(async (modIds: string[]) => {
    if (!selectedProfileId) return;
    await navigateToModSearch(selectedProfileId, modIds, report?.categoryId);
    onClose();
  }, [selectedProfileId, onClose, report?.categoryId]);

  return (
    <div className="mod-analyzer">
      {viewMode === 'scan' && (
        <ScanView
          progress={progress}
          scanning={scanning}
          cancelling={cancelling}
          loading={!!initialCategoryId && !scanning && !report}
          initialFeed={initialFeed}
          categories={categories}
          selectedCategoryId={selectedCategoryId}
          onCategoryChange={setSelectedCategoryId}
          onStart={startScan}
          onPause={pauseScan}
          onResume={resumeScan}
          onCancel={cancelScan}
          onViewHistory={() => setViewMode('history')}
          sessionCount={sessions.length}
          hasLastResult={!!lastSessionId}
          onViewLastResults={() => void viewLastResults()}
          onOpenInWindow={inWindow || !selectedProfileId ? undefined : () => {
            void api.tool.toggleAnalyzerWindow(selectedProfileId).catch(handleError);
          }}
        />
      )}
      {viewMode === 'findings' && report && (
        <FindingsView
          report={report}
          scanning={scanning}
          onNewScan={() => setViewMode('scan')}
          onRescan={() => { void doStartScan(report?.categoryId ?? selectedCategoryId); }}
          onViewHistory={() => setViewMode('history')}
          onDeleteMod={deleteDuplicateMod}
          deletingModId={deletingModId}
          onEditModName={editModName}
          onLocateMods={locateMods}
          onResolveGroup={startResolveGroup}
          onRepairIni={repairIni}
          repairingModId={repairingModId}
          fixTools={fixTools}
          onRunFix={runFix}
          filter={analyzerUi.findingsFilter}
          onFilterChange={analyzerUi.setFindingsFilter}
          search={analyzerUi.searchText}
          onSearchChange={analyzerUi.setSearchText}
        />
      )}
      <ConfirmDialog
        visible={!!resolvingGroup}
        title={t('tools.modAnalyzer.dedupConfirmTitle')}
        okType="danger"
        okText={t('tools.modAnalyzer.dedupConfirmOk', { count: resolvingGroup?.remove.length ?? 0 })}
        onOk={confirmResolveGroup}
        onCancel={() => setResolvingGroup(undefined)}
        content={
          <div className="mod-analyzer__dedup-confirm">
            <div>
              {t('tools.modAnalyzer.dedupConfirmKeep')}: <strong>{resolvingGroup?.keep.modName}</strong>
              {resolvingGroup?.keep.isLoaded && <> ({t('tools.modAnalyzer.loaded')})</>}
            </div>
            <div>{t('tools.modAnalyzer.dedupConfirmDeleteIntro', { count: resolvingGroup?.remove.length ?? 0 })}</div>
            <ul className="mod-analyzer__dedup-confirm-list">
              {resolvingGroup?.remove.map(m => (
                <li key={m.modId}>{m.modName}{m.isLoaded ? ` (${t('tools.modAnalyzer.loaded')})` : ''}</li>
              ))}
            </ul>
            <div className="mod-analyzer__dedup-confirm-hint">{t('tools.modAnalyzer.dedupConfirmHint')}</div>
          </div>
        }
      />
      {viewMode === 'history' && (
        <HistoryView
          sessions={sessions}
          onViewSession={viewSession}
          onDeleteSession={deleteSession}
          onClearAll={clearAll}
          onBack={() => setViewMode(report ? 'findings' : 'scan')}
        />
      )}
    </div>
  );
};

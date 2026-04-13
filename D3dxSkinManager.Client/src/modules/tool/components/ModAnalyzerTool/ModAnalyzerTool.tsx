import React, { useState, useCallback, useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { useSlideInScreen } from '../../../../shared/hooks/useSlideInScreen';
import { useProfile } from '../../../../shared/context/ProfileContext';
import { api } from '../../../../shared/services/ipc';
import { eventBus, Module, ToolsEventType } from '../../../../shared/services/eventBus';
import { handleError } from '../../../../shared/utils/errorHandler';
import { useStableRef } from '../../../../shared/hooks/useStableRef';
import type { FullAnalysisReport, AnalysisProgress, AnalysisSessionSummary } from '../../../../shared/types/analysis.types';
import type { CategoryInfo } from '../../../../shared/types/category.types';
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
  const content = <ModAnalyzerToolInner initialCategoryId={initialCategoryId} />;

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

const ModAnalyzerToolInner: React.FC<{ initialCategoryId?: string }> = ({ initialCategoryId }) => {
  const { selectedProfileId } = useProfile();

  const [viewMode, setViewMode] = useState<ViewMode>('scan');
  const [report, setReport] = useState<FullAnalysisReport>();
  const [scanning, setScanning] = useState(false);
  const [progress, setProgress] = useState<AnalysisProgress>();
  const [categories, setCategories] = useState<CategoryInfo[]>([]);
  const [selectedCategoryId, setSelectedCategoryId] = useState<string | undefined>(initialCategoryId);
  const [sessions, setSessions] = useState<AnalysisSessionSummary[]>([]);

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
  useEffect(() => {
    const unsubProgress = eventBus.subscribe(Module.TOOL, ToolsEventType.MOD_ANALYSIS_PROGRESS, (e) => {
      const payload = e.payload;
      if (!payload) return;
      setProgress(payload);
      if (payload.status === 'running' && !scanningRef.current) {
        setScanning(true);
        setViewMode('scan');
      }
      // Refresh history once per scan so HistoryView shows the new running session
      if (payload.status === 'running' && !historyRefreshedRef.current) {
        historyRefreshedRef.current = true;
        void loadHistory();
      }
      // Patch session summary with live progress counts
      setSessions(prev => prev.map(s =>
        s.id === payload.sessionId
          ? { ...s, analyzedCount: payload.current, healthyCount: payload.healthyCount, warningCount: payload.warningCount, errorCount: payload.errorCount }
          : s
      ));
    });
    const unsubComplete = eventBus.subscribe(Module.TOOL, ToolsEventType.MOD_ANALYSIS_COMPLETE, (e) => {
      setReport(e.payload);
      setScanning(false);
      setProgress(undefined);
      // Only auto-navigate from scan view — don't pull user away from history
      if (viewModeRef.current === 'scan') {
        setViewMode('findings');
      }
      void loadHistory();
    });
    return () => { unsubProgress(); unsubComplete(); };
  }, [loadHistory, scanningRef, viewModeRef]);

  const doStartScan = useCallback(async (categoryId?: string) => {
    if (!selectedProfileId || scanning) return; // Single scan guard
    try {
      setScanning(true);
      setReport(undefined);
      setProgress(undefined);
      setViewMode('scan');
      historyRefreshedRef.current = false;
      await api.tool.startAnalysis(selectedProfileId, categoryId);
    } catch (error: unknown) {
      handleError(error);
      setScanning(false);
    }
  }, [selectedProfileId, scanning]);

  // Auto-start scan when opened from context menu with a category
  useEffect(() => {
    if (initialCategoryId && selectedProfileId && !scanning) {
      setSelectedCategoryId(initialCategoryId);
      void doStartScan(initialCategoryId);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [initialCategoryId]);

  const startScan = useCallback(() => { void doStartScan(selectedCategoryId); }, [doStartScan, selectedCategoryId]);

  const pauseScan = useCallback(async () => {
    if (!selectedProfileId) return;
    try { await api.tool.pauseAnalysis(selectedProfileId); }
    catch (error: unknown) { handleError(error); }
    setScanning(false);
    setProgress(undefined);
    void loadHistory();
  }, [selectedProfileId, loadHistory]);

  const viewSession = useCallback(async (sessionId: string, sessionStatus?: string) => {
    if (!selectedProfileId) return;
    // Running session → go to scan view where progress events are live
    if (sessionStatus === 'running') {
      setViewMode('scan');
      return;
    }
    try {
      const r = await api.tool.getAnalysisReport(selectedProfileId, sessionId);
      setReport(r);
      setViewMode('findings');
    } catch (error: unknown) { handleError(error); }
  }, [selectedProfileId]);

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

  return (
    <div className="mod-analyzer">
      {viewMode === 'scan' && (
        <ScanView
          progress={progress}
          scanning={scanning}
          categories={categories}
          selectedCategoryId={selectedCategoryId}
          onCategoryChange={setSelectedCategoryId}
          onStart={startScan}
          onPause={pauseScan}
          onViewHistory={() => setViewMode('history')}
          sessionCount={sessions.length}
        />
      )}
      {viewMode === 'findings' && report && (
        <FindingsView
          report={report}
          scanning={scanning}
          onNewScan={() => setViewMode('scan')}
          onRescan={startScan}
          onViewHistory={() => setViewMode('history')}
        />
      )}
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

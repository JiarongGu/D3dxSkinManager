import React from "react";
import ReactDOM from "react-dom/client";

import { ModAnalyzerToolInner } from "./modules/tool/components/ModAnalyzerTool/ModAnalyzerTool";
import { AppWrapper } from "./shared/components/AppWrapper";
import { SlideInScreenProvider } from "./shared/context/SlideInScreenContext";
import { SlideInScreenManager } from "./shared/components/common/SlideInScreen";

import "./index.css";

/**
 * Standalone analyzer window (analyzer.html) — a separate WebView2 window opened via
 * ToolFacade ANALYZER_TOGGLE_WINDOW, so the analysis results can sit beside the main window.
 * AppWrapper supplies the profile/theme/i18n; the analyzer content renders full-window (no slide-in).
 */
const AnalyzerApp: React.FC = () => (
  <AppWrapper>
    <SlideInScreenProvider>
      <div className="analyzer-window-root">
        <ModAnalyzerToolInner inWindow onClose={() => { /* the OS window owns its close button */ }} />
      </div>
      <SlideInScreenManager />
    </SlideInScreenProvider>
  </AppWrapper>
);

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <AnalyzerApp />
  </React.StrictMode>,
);

import React from "react";
import ReactDOM from "react-dom/client";

import { ScreenCaptureTool } from "./modules/tool/components/ScreenCaptureTool/ScreenCaptureTool";
import { AppWrapper } from "./shared/components/AppWrapper";

import "./index.css";

const CaptureApp: React.FC = () => {
  return (
    <AppWrapper>
      <ScreenCaptureTool />
    </AppWrapper>
  );
};

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <CaptureApp />
  </React.StrictMode>,
);

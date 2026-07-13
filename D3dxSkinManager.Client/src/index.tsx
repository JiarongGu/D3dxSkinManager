import ReactDOM from 'react-dom/client';
import './index.css';
import App from './App';
import { ErrorBoundary } from './shared/components/ErrorBoundary';
import reportWebVitals from './reportWebVitals';

// Dev-only: install the IPC/event interceptor (window.__d3dx) for the devtools test harness
// (devtools/dev.mjs cdp ipc|events|iplog). Tree-shaken out of production builds.
if (import.meta.env.DEV) {
  void import('./shared/services/devInterceptor').then((m) => m.installDevInterceptor());
}

const root = ReactDOM.createRoot(
  document.getElementById('root') as HTMLElement
);

// NOTE: React.StrictMode intentionally double-invokes mount in DEV (React 19), which remounts every
// antd popup portal on open → dropdowns/selects visibly FLASH open→close→open. It's a dev-only artifact
// (production never double-mounts), but it makes the config editor's selects/pickers flicker. Rendering
// without StrictMode removes the flash; the trade-off is losing React's dev double-invoke checks.
root.render(
  <ErrorBoundary>
    <App />
  </ErrorBoundary>
);

// If you want to start measuring performance in your app, pass a function
// to log results (for example: reportWebVitals(console.log))
// or send to an analytics endpoint. Learn more: https://bit.ly/CRA-vitals
reportWebVitals();

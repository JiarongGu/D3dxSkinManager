import React from 'react';
import ReactDOM from 'react-dom/client';
import './index.css';
import App from './App';
import reportWebVitals from './reportWebVitals';

// Dev-only: install the IPC/event interceptor (window.__d3dx) for the devtools test harness
// (devtools/dev.mjs cdp ipc|events|iplog). Tree-shaken out of production builds.
if (import.meta.env.DEV) {
  void import('./shared/services/devInterceptor').then((m) => m.installDevInterceptor());
}

const root = ReactDOM.createRoot(
  document.getElementById('root') as HTMLElement
);

root.render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
);

// If you want to start measuring performance in your app, pass a function
// to log results (for example: reportWebVitals(console.log))
// or send to an analytics endpoint. Learn more: https://bit.ly/CRA-vitals
reportWebVitals();

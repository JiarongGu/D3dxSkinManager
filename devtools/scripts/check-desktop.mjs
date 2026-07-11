#!/usr/bin/env node
// Desktop-app health checker. Connects to the running D3dxSkinManager desktop app's WebView2 over the Chrome
// DevTools Protocol (CDP) and reports the ACTUAL state of the real app (not the web preview):
//   - whether CSS/styles loaded, whether React rendered, current console errors
//   - `contentReady`: matches of cfg.healthProbe (the app's "real shell rendered" selector)
//   - saves a real screenshot of the desktop WebView2 to devtools/desktop-screenshot.png
//
// Requires the app launched with CDP (devtools/dev.mjs app start — sets the remote-debugging port).
// Port defaults to the persisted launch port (devtools/.cdp-port); pass [port] to override.
// Usage: node devtools/dev.mjs check [port] [--shot]
import { writeFileSync } from 'node:fs';
import { shotPath } from './_capture-util.mjs';
import { readCdpPort } from './_cdp-port.mjs';
import cfg from '../project.config.mjs';

const PORT = Number(process.argv[2]) || readCdpPort();

async function main() {
  let targets;
  try {
    targets = await (await fetch(`http://127.0.0.1:${PORT}/json/list`)).json();
  } catch (e) {
    console.error(`[check-desktop] CDP not reachable on :${PORT} — is the app running with --remote-debugging-port=${PORT}? (${e.message})`);
    process.exit(2);
  }
  const page = targets.find((t) => t.type === 'page' && t.url.includes(cfg.viteUrlMatch))
    || targets.find((t) => t.type === 'page') || targets[0];
  if (!page) { console.error('[check-desktop] no page target found'); process.exit(2); }
  console.log(`[check-desktop] target: "${page.title}" ${page.url}`);

  const ws = new WebSocket(page.webSocketDebuggerUrl);
  const pending = new Map();
  const consoleErrors = [];
  let nextId = 1;
  const send = (method, params = {}) =>
    new Promise((resolve, reject) => {
      const id = nextId++;
      pending.set(id, { resolve, reject });
      ws.send(JSON.stringify({ id, method, params }));
    });

  ws.addEventListener('message', (ev) => {
    const msg = JSON.parse(ev.data);
    if (msg.id && pending.has(msg.id)) {
      const { resolve, reject } = pending.get(msg.id);
      pending.delete(msg.id);
      msg.error ? reject(new Error(msg.error.message)) : resolve(msg.result);
    } else if (msg.method === 'Log.entryAdded' && msg.params.entry.level === 'error') {
      consoleErrors.push(msg.params.entry.text);
    } else if (msg.method === 'Runtime.exceptionThrown') {
      consoleErrors.push('EXCEPTION: ' + (msg.params.exceptionDetails?.exception?.description || msg.params.exceptionDetails?.text || 'unknown'));
    } else if (msg.method === 'Runtime.consoleAPICalled' && msg.params.type === 'error') {
      consoleErrors.push('console.error: ' + msg.params.args.map((a) => a.value ?? a.description ?? '').join(' '));
    }
  });

  await new Promise((res, rej) => { ws.addEventListener('open', res); ws.addEventListener('error', () => rej(new Error('ws error'))); });
  await send('Page.enable');
  await send('Runtime.enable');
  await send('Log.enable');
  await new Promise((r) => setTimeout(r, 800)); // collect a moment of console output

  const diag = `(() => {
    let cssRules = 0, sheets = 0;
    try { sheets = document.styleSheets.length; for (const s of document.styleSheets) { try { cssRules += s.cssRules.length; } catch(e){} } } catch(e){}
    const root = document.getElementById('root');
    const probeSel = ${JSON.stringify(cfg.healthProbe || '')};
    return JSON.stringify({
      url: location.href,
      reactMounted: !!root && root.childElementCount > 0,
      styleSheets: sheets,
      cssRules,
      bodyBg: getComputedStyle(document.body).backgroundColor,
      sidebarStyled: (() => { const el = document.querySelector('nav,aside,[class*="sider"],[class*="Sider"]'); return el ? getComputedStyle(el).backgroundColor : 'no-sidebar'; })(),
      contentReady: probeSel ? document.querySelectorAll(probeSel).length : null,
      textSample: (document.body.innerText || '').replace(/\\s+/g,' ').slice(0, 160)
    });
  })()`;
  const { result } = await send('Runtime.evaluate', { expression: diag, returnByValue: true });
  const report = JSON.parse(result.value);

  try {
    const shot = await send('Page.captureScreenshot', { format: 'png' });
    const out = shotPath('check');
    writeFileSync(out, Buffer.from(shot.data, 'base64'));
    report.screenshot = out;
  } catch (e) { report.screenshotError = e.message; }

  report.consoleErrors = consoleErrors.slice(0, 20);
  // Verdict
  const cssOk = report.cssRules > 50 && report.bodyBg !== 'rgba(0, 0, 0, 0)';
  const contentOk = report.contentReady == null || report.contentReady > 0; // null = no probe configured
  report.verdict = {
    cssLoaded: cssOk,
    reactOk: report.reactMounted,
    contentRendered: contentOk,
    healthy: cssOk && report.reactMounted && contentOk && consoleErrors.length === 0,
  };
  console.log(JSON.stringify(report, null, 2));
  ws.close();
}
main().catch((e) => { console.error('[check-desktop] failed:', e.message); process.exit(1); });

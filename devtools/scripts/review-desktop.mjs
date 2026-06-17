#!/usr/bin/env node
// Feature-review the running desktop app over CDP: navigate each tab, capture the WebView2 render, and
// report React/CSS health + any console errors per screen. Saves devtools/review-<tab>.png for each.
// Usage: node devtools/dev.mjs review [port]
import { writeFileSync } from 'node:fs';
import { shotPath, stamp } from './_capture-util.mjs';
import cfg from '../project.config.mjs';
import { readCdpPort } from './_cdp-port.mjs';
const PORT = Number(process.argv[2]) || readCdpPort();
const RUN = stamp(); // one timestamp for the whole review run → all tabs group together
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function main() {
  const targets = await (await fetch(`http://127.0.0.1:${PORT}/json/list`)).json();
  const page = targets.find((t) => t.type === 'page' && t.url.includes(cfg.viteUrlMatch))
    || targets.find((t) => t.type === 'page') || targets[0];
  const ws = new WebSocket(page.webSocketDebuggerUrl);
  const pending = new Map(); let id = 1; const errors = [];
  ws.addEventListener('message', (ev) => {
    const m = JSON.parse(ev.data);
    if (m.id && pending.has(m.id)) { const p = pending.get(m.id); pending.delete(m.id); m.error ? p.reject(new Error(m.error.message)) : p.resolve(m.result); }
    else if (m.method === 'Runtime.exceptionThrown') errors.push(m.params.exceptionDetails?.exception?.description || m.params.exceptionDetails?.text);
    else if (m.method === 'Log.entryAdded' && m.params.entry.level === 'error') errors.push(m.params.entry.text);
  });
  await new Promise((r, j) => { ws.addEventListener('open', r); ws.addEventListener('error', () => j(new Error('ws'))); });
  const send = (method, params = {}) => new Promise((res, rej) => { const i = id++; pending.set(i, { resolve: res, reject: rej }); ws.send(JSON.stringify({ id: i, method, params })); });
  const evalJs = (e) => send('Runtime.evaluate', { expression: e, returnByValue: true, awaitPromise: true }).then((r) => r.result.value);
  await send('Runtime.enable'); await send('Log.enable'); await send('Page.enable');

  const tabs = cfg.reviewTabs && cfg.reviewTabs.length ? cfg.reviewTabs : ['Mods', 'Tools', 'Settings'];
  const results = [];
  for (const label of tabs) {
    const before = errors.length;
    const clicked = await evalJs(`(()=>{const items=[...document.querySelectorAll('[role="menuitem"], nav a, nav button, aside a, aside button, li, .ant-menu-item')]; const el=items.find(i=>i.textContent.trim()===${JSON.stringify(label)}); if(el){(el.closest('a,button,[role=menuitem],li,.ant-menu-item')||el).click(); return 'ok';} return 'not-found';})()`);
    await sleep(1400);
    const info = JSON.parse(await evalJs(`JSON.stringify({header:(document.querySelector('h1,h2,[class*="header"] [class*="title"]')||{}).textContent||'', textLen:(document.body.innerText||'').length, cssRules:[...document.styleSheets].reduce((n,s)=>{try{return n+s.cssRules.length}catch{return n}},0)})`));
    const shot = await send('Page.captureScreenshot', { format: 'png' });
    const file = shotPath(`review-${label}`, RUN);
    writeFileSync(file, Buffer.from(shot.data, 'base64'));
    results.push({ tab: label, clicked, cssRules: info.cssRules, textLen: info.textLen, newErrors: errors.length - before, file });
    console.log(`${label.padEnd(12)} click=${clicked} css=${info.cssRules} textLen=${info.textLen} errors=${errors.length - before}`);
  }
  console.log('\\nScreenshots saved: ' + results.map((r) => r.file.split(/[\\\\/]/).pop()).join(', '));
  if (errors.length) { console.log('\\n--- console errors ---'); errors.slice(0, 15).forEach((e) => console.log('  ' + String(e).slice(0, 180))); }
  ws.close();
}
main().catch((e) => { console.error('review failed:', e.message); process.exit(1); });

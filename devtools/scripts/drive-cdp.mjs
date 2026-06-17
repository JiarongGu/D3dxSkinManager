#!/usr/bin/env node
// drive-cdp.mjs — tiny CDP driver for the running desktop app (one allow-listed command, no inline
// `node -e`). Connects to the WebView2 page over the Chrome DevTools Protocol (the persisted launch port
// and performs ONE action, so verification steps stay repo-committed + permission-friendly.
//
// Usage:
//   node devtools/dev.mjs cdp open                 click the first library play button (open the player)
//   node devtools/dev.mjs cdp key "]"              dispatch a document keydown (e.g. speed-up hotkey)
//   node devtools/dev.mjs cdp eval "<jsExpr>"      Runtime.evaluate an expression, print its value
//   node devtools/dev.mjs cdp reload [ms]          Page.reload + settle ms (default 3000), then exit
//   node devtools/dev.mjs cdp nav "<TabName>"      click a sidebar tab by label (e.g. "Library")
//   node devtools/dev.mjs cdp menu "<ItemText>"    right-click the 1st library card, click that context
//                                                   -menu item, settle, print the slide-in/modal state
//   node devtools/dev.mjs cdp probe                print current slide-in/modal/player state as JSON
//   node devtools/dev.mjs cdp ipc MOD TYPE [json]  invoke ANY IPC via the dev interceptor
//                                                   (window.__d3dx.call) — bypasses native dialogs
//   node devtools/dev.mjs cdp events [n]           print the last n intercepted events (default 20)
//   node devtools/dev.mjs cdp iplog [n]            print the last n intercepted IPC calls (default 20)
//   node devtools/dev.mjs cdp wait 1500            sleep N ms (no CDP) — for timing between steps
//   node devtools/dev.mjs cdp grab [chrome|menu|chrome-alpha] [label]   copy the latest native bitmap
//                                                   dump → devtools/screenshots/<ts>-<label>.png (no CDP)
//   node devtools/dev.mjs cdp ... 9223             optional trailing CDP port
// `grab` + `wait` exist so the whole verify flow is allow-listed `node devtools/...` calls — no `cp`/`ls`/
// `stat` compound that the permission matcher can't allow. Zero deps (Node 24 fetch/WebSocket/fs).

import { existsSync, mkdirSync, copyFileSync, readdirSync, statSync, writeFileSync, readFileSync } from 'node:fs';
import { resolve, dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import cfg from '../project.config.mjs';
import { shotPath, finalizeShot } from './_capture-util.mjs';
import { readCdpPort } from './_cdp-port.mjs';

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');

const argv = process.argv.slice(2);
// A CDP port is >= 1024; a small numeric arg (e.g. `events 20`) is a count, not the port.
const port = Number(argv.find((a) => /^\d+$/.test(a) && Number(a) >= 1024) || readCdpPort());
const action = argv[0];
const arg = argv[1];

// In-page reference to the dev interceptor global (from project.config.mjs), e.g. window["__d3dx"].
const G = `window[${JSON.stringify(cfg.devGlobal)}]`;

// Expression that reports the current overlay state — slide-in screen, AntD modal, native player.
const PROBE = `(()=>{const si=document.querySelector('.slide-in-screen-container');const m=document.querySelector('.ant-modal-content,[role=dialog]');return JSON.stringify({slideIn:!!si,slideInTitle:si?(si.querySelector('.slide-in-screen-title')||{}).textContent:null,panelW:si?getComputedStyle(si.querySelector('.slide-in-screen-panel')).width:null,modal:!!m});})()`;

const EXPRS = {
  // The app renders the play icon inside a clickable element (not a <button>) — walk up + click the chain.
  open: `(()=>{const ic=document.querySelector(${JSON.stringify(cfg.playSelector)});if(!ic)return "no-play-icon";let el=ic;for(let i=0;i<4&&el;i++){el.click&&el.click();el=el.parentElement;}return "opened";})()`,
  probe: PROBE,
};

// Async IIFE: right-click the first library card, click the named context-menu item, settle, then PROBE.
const menuExpr = (item) => `(async()=>{const sleep=ms=>new Promise(r=>setTimeout(r,ms));const c=document.querySelector('.library-grid [class*=card]')||document.querySelector('[class*=card]');if(!c)return 'no-card';const r=c.getBoundingClientRect();c.dispatchEvent(new MouseEvent('contextmenu',{bubbles:true,clientX:r.x+40,clientY:r.y+40}));await sleep(450);const it=[...document.querySelectorAll('.context-menu-item')].find(e=>e.textContent.includes(${JSON.stringify(item)}));if(!it)return 'no-item:'+[...document.querySelectorAll('.context-menu-item')].map(e=>e.textContent.trim()).join('|');it.click();await sleep(1300);${PROBE.replace(/^\(\(\)=>\{/, '').replace(/\}\)\(\)$/, '')}})()`;

// Click a left-sidebar tab by its label text.
const navExpr = (tab) => `(()=>{const n=[...document.querySelectorAll('*')].find(e=>e.children.length===0&&e.textContent.trim()===${JSON.stringify(tab)}&&e.closest('[class*=nav],[class*=sider],aside'));if(!n)return 'no-tab';(n.closest('a,button,li,div[role]')||n).click();return 'nav '+${JSON.stringify(tab)};})()`;

// Locate the freshest native bitmap dump under bin/**/data/logs and copy it to a timestamped screenshot.
function grab() {
  const kind = (arg && !/^\d+$/.test(arg)) ? arg : 'chrome';
  const label = argv[2] && !/^\d+$/.test(argv[2]) ? argv[2] : kind;
  const fileName = { chrome: 'chrome-last.png', 'chrome-alpha': 'chrome-last-alpha.png', menu: 'menu-last.png' }[kind] || `${kind}.png`;
  // search bin/Debug/**/data/logs
  const logsRoots = [];
  const binDir = resolve(repoRoot, cfg.binDir);
  const walk = (d, depth) => {
    if (depth > 4 || !existsSync(d)) return;
    for (const e of readdirSync(d, { withFileTypes: true })) {
      if (!e.isDirectory()) continue;
      const p = join(d, e.name);
      if (e.name === 'logs') logsRoots.push(p); else walk(p, depth + 1);
    }
  };
  walk(binDir, 0);
  let best = null;
  for (const lr of logsRoots) {
    const f = join(lr, fileName);
    if (existsSync(f)) { const m = statSync(f).mtimeMs; if (!best || m > best.m) best = { f, m }; }
  }
  if (!best) { console.error(`drive-cdp: no ${fileName} found under ${binDir}`); process.exit(2); }
  // The dump is rewritten every render — a copy can catch a mid-write (0-byte) file. Guard ONLY against
  // an empty file (quick retries); do NOT wait for size-stability — that would wait out a transient like
  // the OSD toast (its expiry re-render would read as "not settled yet").
  for (let i = 0; i < 8 && statSync(best.f).size === 0; i++) { const u = Date.now() + 60; while (Date.now() < u) { /* spin */ } }
  const out = shotPath(label); // <prefix>-<ts>-<label>.png (centralized naming + prune)
  copyFileSync(best.f, out);
  finalizeShot(out); // downscale under the read limit + prune old shots (screenshot-hygiene rule)
  const age = Math.round((Date.now() - best.m) / 1000);
  console.log(`drive-cdp: grabbed ${fileName} (age ${age}s) → ${out}`);
}

async function main() {
  if (action === 'grab') { grab(); return; }
  if (action === 'wait') { await new Promise((r) => setTimeout(r, Number(arg) || 1000)); console.log(`drive-cdp: waited ${Number(arg) || 1000}ms`); return; }
  const list = await (await fetch(`http://127.0.0.1:${port}/json/list`)).json();
  const page = list.find((t) => t.type === 'page' && t.url.includes(cfg.viteUrlMatch)) || list.find((t) => t.type === 'page');
  if (!page) { console.error('drive-cdp: no CDP page (is the app running with --remote-debugging-port?)'); process.exit(2); }

  const ws = new WebSocket(page.webSocketDebuggerUrl);
  let id = 0;
  const send = (method, params) => new Promise((res) => {
    const i = ++id;
    const h = (e) => { const d = JSON.parse(e.data); if (d.id === i) { ws.removeEventListener('message', h); res(d.result); } };
    ws.addEventListener('message', h);
    ws.send(JSON.stringify({ id: i, method, params }));
  });
  await new Promise((r) => ws.addEventListener('open', r));
  await send('Runtime.enable', {});

  // Reload the Vite page + settle, so a frontend change is picked up in ONE allow-listed call
  // (replaces the `eval "location.reload()" + node -e setTimeout` chain that tripped permissions).
  if (action === 'reload') {
    await send('Page.enable', {});
    await send('Page.reload', { ignoreCache: false });
    const ms = Number(arg) || 3000;
    await new Promise((r) => setTimeout(r, ms));
    console.log(`drive-cdp: reloaded + settled ${ms}ms`);
    ws.close();
    return;
  }

  // DOM screenshot via CDP (the React UI — NOT the native video/overlay, which aren't in the DOM). Use
  // for pure-DOM screens like Settings: `drive-cdp.mjs shot <label>`.
  if (action === 'shot') {
    await send('Page.enable', {});
    const res = await send('Page.captureScreenshot', { format: 'png' });
    const label = (arg && !/^\d+$/.test(arg)) ? arg : 'dom';
    const out = shotPath(label); // <prefix>-<ts>-<label>.png (centralized naming + prune)
    writeFileSync(out, Buffer.from(res.data, 'base64'));
    finalizeShot(out); // downscale under the read limit + prune old shots (screenshot-hygiene rule)
    console.log(`drive-cdp: shot → ${out}`);
    ws.close();
    return;
  }

  let expr;
  let awaitPromise = false;
  if (action === 'open') expr = EXPRS.open;
  else if (action === 'probe') expr = EXPRS.probe;
  else if (action === 'nav') expr = navExpr(arg);
  else if (action === 'menu') { expr = menuExpr(arg); awaitPromise = true; }
  else if (action === 'ipc') {
    // window[devGlobal].call(MODULE, TYPE, payload?) — drive any IPC, bypassing native dialogs/UI.
    // (devGlobal is the dev interceptor from project.config.mjs — shared/services/devInterceptor.ts.)
    // Pass the payload as a JSON string parsed IN the page (JSON.parse) — NOT inlined raw. Inlining
    // raw loses a backslash layer through JSON.stringify(expression), which corrupted Windows paths
    // (e.g. "Y:\MMD" → "Y:MMD"); the parse round-trip preserves them.
    const mod = JSON.stringify(argv[1] || '');
    const type = JSON.stringify(argv[2] || '');
    const payloadArg = argv[3] ? `JSON.parse(${JSON.stringify(argv[3])})` : 'undefined';
    expr = `(${G}?${G}.call(${mod},${type},${payloadArg}).then(r=>JSON.stringify(r)).catch(e=>'IPC-ERROR: '+e.message):Promise.resolve('NO-INTERCEPTOR (run a DEV build)'))`;
    awaitPromise = true;
  }
  else if (action === 'events') expr = `(${G}?JSON.stringify(${G}.recentEvents(${Number(arg) || 20}),null,0):'NO-INTERCEPTOR')`;
  else if (action === 'iplog') expr = `(${G}?JSON.stringify(${G}.recentIpc(${Number(arg) || 20}),null,0):'NO-INTERCEPTOR')`;
  else if (action === 'key') expr = `(()=>{document.dispatchEvent(new KeyboardEvent("keydown",{key:${JSON.stringify(arg)},bubbles:true}));return "key ${arg}";})()`;
  else if (action === 'eval') { expr = arg; awaitPromise = true; } // await if the expr returns a Promise
  else if (action === 'evalfile') { expr = readFileSync(resolve(repoRoot, arg), 'utf8'); awaitPromise = true; } // run a JS file's expr (e.g. an async IIFE returning a JSON-able value) — for big probes that don't fit a shell arg
  else { console.error(`drive-cdp: unknown action "${action}" — use open|key|eval|evalfile|reload|nav|menu|probe|ipc|events|iplog|shot|grab|wait`); ws.close(); process.exit(1); }

  const r = await send('Runtime.evaluate', { expression: expr, returnByValue: true, awaitPromise });
  console.log(`drive-cdp: ${action} → ${JSON.stringify(r?.result?.value)}`);
  ws.close();
}
main().catch((e) => { console.error('drive-cdp:', e.message); process.exit(1); });

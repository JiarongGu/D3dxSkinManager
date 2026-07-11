#!/usr/bin/env node
// veil-eval.mjs — measure the content-veil detectors against GameBanana's own content ratings.
//
// GameBanana's Subfeed marks content-rated (NSFW) mods with _sInitialVisibility = "warn"/"hide",
// unrated ones "show" — free ground-truth labels, no image eyeballing. The harness:
//   1. fetches N Subfeed pages for a game (default ZZZ 19567),
//   2. asks the RUNNING dev app (CDP → window.__d3dx SYSTEM/CONTENT_VEIL_INSPECT) for verdicts +
//      metrics on each card image (proxy://image/?u=… — same urls the UI uses),
//   3. prints a confusion matrix for the MODEL verdict and the HEURISTIC (recomputed from metrics),
//      plus the mismatches (title, label, verdict, prob) for eyeball follow-up.
//
//   node devtools/dev.mjs veil [pages] [gameId]      (defaults: 3 pages, game 19567)
//
// Images are fetched through the app's remote-image cache (sha1-named, reused by the UI later) —
// a few pages ≈ 150 thumbnails, polite. Requires the dev app running (node devtools/dev.mjs app start).

// Modes:
//   node devtools/dev.mjs veil [pages] [gameId]   — GB Subfeed ratings as WEAK labels (broad sweep)
//   node devtools/dev.mjs veil labels             — the labeled image corpus (folder = label)
//   node devtools/dev.mjs veil sweep              — GRID-SEARCH ContentVeilTuning over the corpus
//                                                   (per-request overrides — no rebuild per config)

import { readFileSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { readCdpPort } from './_cdp-port.mjs';

const sweepMode = process.argv[2] === 'sweep';
const benchMode = process.argv[2] === 'bench';
const dumpMode = process.argv[2] === 'dump';
const labelsMode = process.argv[2] === 'labels' || sweepMode || dumpMode;
const pages = Number(process.argv[2]) || 3;
const gameId = process.argv[3] || '19567';
const port = readCdpPort();
const here = dirname(fileURLToPath(import.meta.url));


async function fetchSubfeed(page) {
  const url = `https://gamebanana.com/apiv11/Game/${gameId}/Subfeed?_nPage=${page}`;
  const res = await fetch(url, { headers: { 'User-Agent': 'D3dxSkinManager' } });
  if (!res.ok) throw new Error(`subfeed page ${page}: HTTP ${res.status}`);
  const json = await res.json();
  return (json._aRecords ?? [])
    .filter((r) => r._sModelName === 'Mod' && r._aPreviewMedia?._aImages?.length)
    .map((r) => {
      const img = r._aPreviewMedia._aImages[0];
      const file = img._sFile530 ?? img._sFile220 ?? img._sFile;
      return {
        title: r._sName,
        label: (r._sInitialVisibility ?? 'show') !== 'show', // true = site-rated sensitive
        imageUrl: `${img._sBaseUrl.replace(/\/$/, '')}/${file}`,
      };
    });
}

// ONE persistent CDP socket for the whole run (a sweep makes 1000+ eval calls — per-call sockets
// eventually get refused) + reconnect-and-retry on transient failures.
let cdp = null;
async function cdpConnect() {
  const list = await (await fetch(`http://127.0.0.1:${port}/json/list`)).json();
  const page = list.find((t) => t.type === 'page' && t.url.includes('3517')) || list.find((t) => t.type === 'page');
  if (!page) throw new Error('no CDP page — is the dev app running?');
  const ws = new WebSocket(page.webSocketDebuggerUrl);
  await new Promise((res, rej) => { ws.addEventListener('open', res); ws.addEventListener('error', rej); });
  let id = 0;
  const send = (method, params) => new Promise((res, rej) => {
    const i = ++id;
    const t = setTimeout(() => rej(new Error('CDP timeout: ' + method)), 120_000);
    const h = (e) => {
      const d = JSON.parse(e.data);
      if (d.id === i) { clearTimeout(t); ws.removeEventListener('message', h); res(d.result); }
    };
    ws.addEventListener('message', h);
    ws.send(JSON.stringify({ id: i, method, params }));
  });
  await send('Runtime.enable', {});
  cdp = { ws, send };
}

async function cdpEval(expr) {
  for (let attempt = 0; attempt < 3; attempt++) {
    try {
      if (!cdp || cdp.ws.readyState !== 1) await cdpConnect();
      const r = await cdp.send('Runtime.evaluate', { expression: expr, awaitPromise: true, returnByValue: true });
      if (!r) throw new Error('empty CDP result');
      if (r.exceptionDetails) throw new Error(`eval failed: ${r.exceptionDetails.text} ${r.exceptionDetails.exception?.description ?? ''}`);
      return r.result.value;
    } catch (e) {
      try { cdp?.ws.close(); } catch { /* ignore */ }
      cdp = null;
      if (attempt === 2) throw e;
      await new Promise((res) => setTimeout(res, 500));
    }
  }
}

function confusion(name, rows, predict) {
  let tp = 0, fp = 0, tn = 0, fn = 0;
  for (const r of rows) {
    const p = predict(r);
    if (p && r.label) tp++;
    else if (p && !r.label) fp++;
    else if (!p && !r.label) tn++;
    else fn++;
  }
  const acc = ((tp + tn) / rows.length * 100).toFixed(1);
  const recall = tp + fn ? (tp / (tp + fn) * 100).toFixed(1) : 'n/a';
  const precision = tp + fp ? (tp / (tp + fp) * 100).toFixed(1) : 'n/a';
  console.log(`\n${name}: acc ${acc}%  recall(NSFW) ${recall}%  precision ${precision}%   TP ${tp} FP ${fp} TN ${tn} FN ${fn}  (n=${rows.length})`);
  return { tp, fp, tn, fn };
}

// SPEED benchmark: time the veil (plugin + CV) over N real cached remote images. Measures the
// hot path the browse grid hits. `node devtools/dev.mjs veil bench [count]`.
if (benchMode) {
  const { readdirSync, statSync } = await import('node:fs');
  const count = Number(process.argv[3]) || 200;
  const cacheDir = resolve(
    here, '..', '..',
    'D3dxSkinManager/bin/Debug/net10.0-windows/data/remote-images',
  );
  const files = readdirSync(cacheDir)
    .filter((f) => /\.(jpg|jpeg|png|webp|gif)$/i.test(f))
    .map((f) => resolve(cacheDir, f))
    .slice(0, count);
  console.log(`bench: ${files.length} cached remote images`);

  // Cold (fresh verdict cache each file is new) — the real cost. Batches of 20 mirror the UI.
  const t0 = Date.now();
  let done = 0;
  for (let i = 0; i < files.length; i += 20) {
    const batch = files.slice(i, i + 20);
    await cdpEval(
      `window.__d3dx.call('SYSTEM','CONTENT_VEIL_INSPECT', ${JSON.stringify({ urls: batch })})`,
    );
    done += batch.length;
    process.stdout.write(`  ${done}/${files.length}\r`);
  }
  const ms = Date.now() - t0;
  console.log(`\ncold: ${ms}ms total, ${(ms / files.length).toFixed(1)}ms/image (${files.length} imgs, batches of 20)`);

  // Warm (host (path,mtime) cache) — repeat the first batch.
  const t1 = Date.now();
  await cdpEval(`window.__d3dx.call('SYSTEM','CONTENT_VEIL_INSPECT', ${JSON.stringify({ urls: files.slice(0, 20) })})`);
  console.log(`warm (cached): ${Date.now() - t1}ms for 20`);
  process.exit(0);
}

const records = [];
if (labelsMode) {
  // Labeled image corpus: devtools/fixtures/veil/{positive|negative}/ — folder = label, so the
  // detector is tested DIRECTLY on files (drop any image in by hand to grow the set). Cases in
  // veil-labels.json that have no snapshot yet are resolved via the index + downloaded once.
  const { mkdirSync, existsSync, writeFileSync, readdirSync } = await import('node:fs');
  const posDir = resolve(here, '..', 'fixtures', 'veil', 'positive');
  const negDir = resolve(here, '..', 'fixtures', 'veil', 'negative');
  mkdirSync(posDir, { recursive: true });
  mkdirSync(negDir, { recursive: true });

  const slug = (t) => t.replace(/[^\p{L}\p{N}]+/gu, '_').replace(/^_+|_+$/g, '').slice(0, 60);
  const extOf = (buf) =>
    buf[0] === 0xff && buf[1] === 0xd8 ? '.jpg'
    : buf[0] === 0x89 && buf[1] === 0x50 ? '.png'
    : buf[0] === 0x52 && buf[1] === 0x49 ? '.webp'
    : buf[0] === 0x47 && buf[1] === 0x49 ? '.gif'
    : '.jpg';
  const spec = JSON.parse(readFileSync(resolve(here, '..', 'fixtures', 'veil-labels.json'), 'utf8'));
  const profileId = await cdpEval(
    "window.__d3dx.call('PROFILE','GET_ALL',{}).then(r=>(r.profiles||r)[0].id)",
  );
  for (const c of spec.cases) {
    const dir = c.label === 'sensitive' ? posDir : negDir;
    const base = slug(c.title);
    if (readdirSync(dir).some((f) => f.startsWith(base + '.'))) continue;
    const q = { sourceId: c.source, listId: c.list, search: c.search, page: 1, pageSize: 20 };
    const res = await cdpEval(
      `window.__d3dx.call('REMOTE','INDEX_QUERY', ${JSON.stringify(q)}, ${JSON.stringify(profileId)})`,
    );
    const hit = (res.entries ?? []).find((e) => e.title === c.title);
    if (!hit) { console.log(`  (label case not in index, no snapshot: "${c.title}")`); continue; }
    try {
      const img = await fetch(hit.imageUrl, { headers: { 'User-Agent': 'D3dxSkinManager' } });
      if (!img.ok) throw new Error('HTTP ' + img.status);
      const buf = Buffer.from(await img.arrayBuffer());
      writeFileSync(resolve(dir, base + extOf(buf)), buf);
      console.log(`  snapshot: ${c.label} ← "${c.title}"`);
    } catch (e) {
      console.log(`  (snapshot failed for "${c.title}": ${e.message})`);
    }
  }

  for (const [dir, label] of [[posDir, true], [negDir, false]]) {
    for (const f of readdirSync(dir)) {
      records.push({ title: f, label, path: resolve(dir, f) });
    }
  }
  console.log(`labels: ${records.length} images (${records.filter((r) => r.label).length} positive)`);
} else {
  for (let p = 1; p <= pages; p++) {
    const cards = await fetchSubfeed(p);
    records.push(...cards);
    console.log(`page ${p}: ${cards.length} mods (${cards.filter((c) => c.label).length} content-rated)`);
  }
}

// Ask the app in batches (metric analysis + first-time image fetches happen backend-side).
// Labeled-corpus records carry a LOCAL file path (the service accepts bare paths).
const urlOf = (c) => c.path ?? `proxy://image/?u=${encodeURIComponent(c.imageUrl)}`;

async function inspectAll(tuning) {
  const out = {};
  for (let i = 0; i < records.length; i += 20) {
    const batch = records.slice(i, i + 20).map(urlOf);
    const payload = tuning ? { urls: batch, tuning } : { urls: batch };
    const res = await cdpEval(
      `window.__d3dx.call('SYSTEM','CONTENT_VEIL_INSPECT', ${JSON.stringify(payload)})`,
    );
    Object.assign(out, res.metrics);
    if (!tuning) process.stdout.write(`inspected ${Math.min(i + 20, records.length)}/${records.length}\r`);
  }
  return out;
}

if (sweepMode) {
  // Grid-search ContentVeilTuning over the labeled corpus. FP is the hard constraint (labeled-safe
  // images must never veil); among zero/low-FP configs, maximize recall.
  // Adjust the axes to whatever is currently in question — keep the grid ≤ a few hundred configs.
  // CV recall knobs. Target: RECALL-first (>90%) accepting ~80-85% negatives (i.e. FP up to ~15-20%).
  // AI-plugin threshold sweep. With the content-veil AI plugin enabled it DECIDES the verdict; the only
  // knob is the confidence cut. Aim: 100% recall on the positive set (user directive 2026-07-12), then
  // read the negative cost. Lower = more recall, more FP.
  const GRID = {
    pluginMinConfidence: [0.50, 0.55, 0.60, 0.65, 0.70, 0.75, 0.80, 0.85, 0.90],
  };
  const keys = Object.keys(GRID);
  const configs = keys.reduce((acc, k) => acc.flatMap((c) => GRID[k].map((v) => ({ ...c, [k]: v }))), [{}]);
  const posN = records.filter((r) => r.label).length;
  const negN = records.length - posN;
  console.log(`sweeping ${configs.length} configs over ${records.length} images (${posN} pos / ${negN} neg)…`);

  const results = [];
  for (let ci = 0; ci < configs.length; ci++) {
    const m = await inspectAll(configs[ci]);
    let fp = 0, fn = 0, tp = 0;
    for (const r of records) {
      const k = m[urlOf(r)];
      const pred = k?.verdict === 'sensitive';
      if (pred && !r.label) fp++;
      else if (!pred && r.label) fn++;
      else if (pred && r.label) tp++;
    }
    const recall = tp / posN, neg = (negN - fp) / negN;
    results.push({ c: configs[ci], fp, fn, tp, recall, neg });
    process.stdout.write(`config ${ci + 1}/${configs.length} (recall=${(recall * 100).toFixed(0)}% neg=${(neg * 100).toFixed(0)}%)   \r`);
  }
  console.log('');
  // The frontier we want: negatives >= 80% (FP small), then MAX recall. Print that ranking + the
  // overall best-recall configs so the ceiling is visible either way.
  const fmt = (r) => `recall ${(r.recall * 100).toFixed(0)}% neg ${(r.neg * 100).toFixed(0)}%  (tp=${r.tp} fp=${r.fp} fn=${r.fn})  ${JSON.stringify(r.c)}`;
  const within = results.filter((r) => r.neg >= 0.80).sort((a, b) => b.recall - a.recall || a.fp - b.fp);
  console.log('\nBest RECALL with negatives >= 80%:');
  for (const r of within.slice(0, 12)) console.log('  ' + fmt(r));
  console.log('\nAbsolute best recall (any FP — shows the CV ceiling):');
  for (const r of [...results].sort((a, b) => b.recall - a.recall || a.fp - b.fp).slice(0, 6)) console.log('  ' + fmt(r));
  process.exit(0);
}

const metrics = await inspectAll(null);
console.log('');

const rows = records
  .map((r) => ({ ...r, m: metrics[urlOf(r)] }))
  .filter((r) => r.m); // unresolvable images excluded

console.log(`resolved ${rows.length}/${records.length} images`);

// FEATURE DUMP — per-image shape features for pos vs neg, to see what (if anything) separates the
// point-INVISIBLE positives from high-skin negatives. `node devtools/dev.mjs veil dump`.
if (dumpMode) {
  const line = (r) => {
    const m = r.m;
    const contig = m.fgSkinRatio > 0 ? (m.largestFgRegion / m.fgSkinRatio) : 0;
    return `${r.label ? 'POS' : 'neg'}  fg=${m.fgSkinRatio.toFixed(2)} big=${m.largestFgRegion.toFixed(2)} contig=${contig.toFixed(2)} regions=${m.regionCount} pts=${m.pointCount} inreg=${m.inRegionPointCount} zpts=${m.zoomPointCount} zscore=${(m.zoomMaxPointScore ?? 0).toFixed(2)} score=${(m.maxPointScore ?? 0).toFixed(2)} v=${m.verdict}`;
  };
  const pos = rows.filter((r) => r.label), neg = rows.filter((r) => !r.label);
  console.log('\n=== POSITIVES ===');
  for (const r of pos) console.log('  ' + line(r));
  console.log('\n=== NEGATIVES ===');
  for (const r of neg) console.log('  ' + line(r));
  // quick separation view: positives with NO point signal (the hard set) vs negatives, on big+contig
  const hard = pos.filter((r) => r.m.pointCount === 0 && (r.m.zoomPointCount ?? 0) === 0);
  console.log(`\nPoint-INVISIBLE positives (${hard.length}/${pos.length}) big/contig histogram:`);
  for (const r of hard.sort((a, b) => b.m.largestFgRegion - a.m.largestFgRegion)) console.log('  ' + line(r));
  process.exit(0);
}

confusion('VERDICT (as served)', rows, (r) => r.m.verdict === 'sensitive');
confusion('NAIVE skin-count (skinRatio>=0.5)', rows, (r) => r.m.skinRatio >= 0.5);

// (The offline rule sweep was removed: the zoom pass re-analyzes crops backend-side, so candidate
// rules can no longer be replayed from pass-1 metrics alone. Tune consts → rebuild → re-run.)

// NOTE the labels are WEAK on purpose: GB rates whole MODS (suggestive included) while the
// verdict targets EXPLICIT THUMBNAILS only — expect "FNs" on rated mods with tame/suggestive
// thumbnails. Judge FPs strictly, FNs loosely — and eyeball the FP titles.

console.log('\nMismatches (verdict vs label):');
for (const r of rows) {
  const p = r.m.verdict === 'sensitive';
  if (p !== r.label) {
    const m = r.m;
    const zoom = m.zoomApplied
      ? ` zoom[pts=${m.zoomPointCount}${m.zoomPaired ? ' PAIR' : ''} s=${(m.zoomMaxPointScore ?? 0).toFixed(2)}]`
      : '';
    const plugin = m.pluginConfidence == null ? '' : ` ai=${m.pluginConfidence.toFixed(2)}`;
    console.log(
      `  [${r.label ? 'FN' : 'FP'}]${m.verdictRule ? ' rule=' + m.verdictRule : ''}` +
      ` pts=${m.pointCount}${m.pairedPoints ? ' PAIR' : ''}` +
      ` score=${(m.maxPointScore ?? 0).toFixed(2)} fg=${m.fgSkinRatio.toFixed(2)}` +
      ` big=${m.largestFgRegion.toFixed(2)}${zoom}${plugin} "${r.title}"`);
  }
}

// Rule attribution for the HITS too — shows which rules carry the recall.
const ruleCounts = {};
for (const r of rows) {
  if (r.m.verdict === 'sensitive' && r.label) {
    ruleCounts[r.m.verdictRule ?? '?'] = (ruleCounts[r.m.verdictRule ?? '?'] ?? 0) + 1;
  }
}
console.log('\nTrue positives by rule: ' + JSON.stringify(ruleCounts));
process.exit(0); // the persistent CDP socket would otherwise keep node alive

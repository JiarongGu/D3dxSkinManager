#!/usr/bin/env node
// hui-ip-probe.mjs — discover the API/download method of huihui's IP/VPN Hui盘 mirror before writing a
// resolver for it. The main Hui盘 (cloudreve.huihui123.org) is Cloudreve v4 (/api/v4). Some mods instead
// link a raw-IP mirror like http://174.136.207.5/#s/<key> — the user reports it uses a DIFFERENT download
// method, so probe its version/shape instead of assuming v4. Plain HTTP (IP, no TLS). Zero-dep (Node 24).
//
//   node devtools/hui-ip-probe.mjs "http://174.136.207.5/#s/_1I87x0w"
//
// Prints: server headers, and the response of the candidate Cloudreve v3/v4 share endpoints.
const link = process.argv[2] || 'http://174.136.207.5/#s/_1I87x0w';
const m = link.match(/^(https?:\/\/[^/]+)\/#?s\/([^/?#]+)/);
if (!m) { console.error('not a hui IP share link:', link); process.exit(1); }
const origin = m[1], key = m[2];
console.log(`origin=${origin}  key=${key}\n`);

async function probe(label, url, opts = {}) {
  try {
    const ctl = AbortSignal.timeout(12000);
    const res = await fetch(url, { signal: ctl, redirect: 'manual', ...opts });
    const server = res.headers.get('server') || res.headers.get('x-powered-by') || '(none)';
    const ct = res.headers.get('content-type') || '';
    let body = await res.text();
    const snippet = body.length > 400 ? body.slice(0, 400) + '…' : body;
    console.log(`[${label}] ${opts.method || 'GET'} ${url}\n  ${res.status} ${res.statusText} · server=${server} · type=${ct}\n  ${snippet.replace(/\n/g, ' ')}\n`);
    return { status: res.status, body, ct };
  } catch (e) {
    console.log(`[${label}] ${url}\n  FAILED: ${e.name} ${e.message}\n`);
    return null;
  }
}

(async () => {
  const root = await probe('root', `${origin}/`);
  if (root?.body) {
    const html = root.body;
    const title = (html.match(/<title>([\s\S]*?)<\/title>/i) || [])[1];
    const scripts = [...html.matchAll(/<script[^>]+src="([^"]+)"/gi)].map(m => m[1]);
    const links = [...html.matchAll(/<link[^>]+href="([^"]+)"/gi)].map(m => m[1]);
    const apiish = [...new Set([...html.matchAll(/["'`]([^"'`]*\/(?:api|download|share|s|d|f)\/[^"'`]*)["'`]/gi)].map(m => m[1]))].slice(0, 20);
    const generator = (html.match(/name="generator"\s+content="([^"]*)"/i) || [])[1];
    console.log(`  title=${JSON.stringify(title)}  generator=${JSON.stringify(generator)}`);
    console.log(`  scripts: ${scripts.join(' | ') || '(none)'}`);
    console.log(`  links: ${links.join(' | ') || '(none)'}`);
    console.log(`  api-ish strings: ${apiish.join(' | ') || '(none)'}\n`);
    // Fetch the kodbox APP bundle (main.js) and grep for its index.php? share/download routes.
    const bundle = scripts.find(s => /main\.js/i.test(s)) || scripts.find(s => /\.js(\?|$)/i.test(s));
    if (bundle) {
      const u = bundle.startsWith('http') ? bundle : origin + (bundle.startsWith('/') ? '' : '/') + bundle;
      const js = await probe('bundle', u);
      if (js?.body) {
        const routes = [...new Set([...js.body.matchAll(/index\.php\?([A-Za-z0-9_\/&=]+)/gi)].map(m => m[1]))].slice(0, 60);
        console.log(`  index.php routes: ${routes.join(' | ') || '(none)'}`);
        const shareish = [...new Set([...js.body.matchAll(/[?&'"`]((?:explorer\/)?share\/[A-Za-z0-9_]+)/gi)].map(m => m[1]))].slice(0, 40);
        console.log(`  share actions: ${shareish.join(' | ') || '(none)'}`);
      }
    }
  }
  // --- kodbox share API: discover param name + response shape for explorer/share/get ---
  console.log('--- kodbox share/get probes ---');
  const form = (o) => new URLSearchParams(o).toString();
  const post = (action, body) => probe(`POST ${action}`, `${origin}/index.php?${action}`, {
    method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' }, body: form(body),
  });
  const got = await post('explorer/share/get', { shareID: key });
  let info = null;
  try { info = JSON.parse(got.body).data; } catch {}
  if (info) {
    console.log('  share/get data keys: ' + Object.keys(info).join(', '));
    console.log('  sourceInfo: ' + JSON.stringify(info.sourceInfo));
    console.log(`  title=${info.title} isLink=${info.isLink} isFolder=${info.isFolder ?? '?'}`);
  }
  // Single-file share download: the path from sourceInfo.
  const src = info?.sourceInfo || {};
  const filePath = src.path || '/' + (info?.title || '');
  console.log(`  file path token: ${filePath}  size=${src.size}`);

  // Can fileDownload be done over GET (so the resolver can hand a plain URL to IDownloadService)?
  const getUrl = `${origin}/index.php?explorer/share/fileDownload&shareID=${encodeURIComponent(key)}&path=${encodeURIComponent(filePath)}`;
  const g = await fetch(getUrl, { signal: AbortSignal.timeout(20000), redirect: 'manual' });
  const clen = g.headers.get('content-length');
  const buf = Buffer.from(await g.arrayBuffer());
  const isZip = buf.length >= 2 && buf[0] === 0x50 && buf[1] === 0x4b;
  console.log(`  GET fileDownload → ${g.status} ${g.headers.get('content-type')} content-length=${clen} bytes=${buf.length} isZip=${isZip}`);
  console.log(`  >>> GET URL: ${getUrl}`);
  console.log('done — kodbox share/get → fileDownload (GET) validated.');
})();

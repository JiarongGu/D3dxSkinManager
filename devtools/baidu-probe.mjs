// baidu-probe.mjs — recon the ANONYMOUS half of a pan.baidu.com share (verify password → share
// metadata/list) to ground BaiduShareResolver. Uses a real kekehxl share link. The transfer(转存)+
// download half needs a logged-in BDUSS cookie (user logs in via the in-app window) and is NOT probed
// here. Zero-dep (Node 24 global fetch). Usage: node devtools/baidu-probe.mjs [shareUrl] [pwd]
const SHARE = process.argv[2] || 'https://pan.baidu.com/s/1xmj5hE9oECTfls2zHJWn7A?pwd=keke';
const PWD = process.argv[3] || (new URL(SHARE).searchParams.get('pwd')) || 'keke';
const UA = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36';

const jar = new Map();
const setCookies = (r) => { const a = r.headers.getSetCookie ? r.headers.getSetCookie() : []; for (const c of a) { const kv = c.split(';')[0], i = kv.indexOf('='); if (i > 0) jar.set(kv.slice(0, i).trim(), kv.slice(i + 1).trim()); } };
const ck = () => [...jar].map(([k, v]) => `${k}=${v}`).join('; ');
async function req(url, opt = {}) {
  const r = await fetch(url, { redirect: 'manual', ...opt, headers: { 'User-Agent': UA, 'Cookie': ck(), Referer: 'https://pan.baidu.com/', ...(opt.headers || {}) } });
  setCookies(r); return { s: r.status, loc: r.headers.get('location'), ct: r.headers.get('content-type'), b: await r.text().catch(() => '') };
}

// the surl = the base62 after "/s/1" (Baidu's share short id; the leading "1" is a version marker).
const path = new URL(SHARE).pathname; // /s/1xxxx
const surl = path.replace(/^\/s\/1?/, '');
console.log('shareUrl:', SHARE, '\npwd:', PWD, '\nsurl:', surl);

// 1) hit the share page first to seed BAIDUID/anti-bot cookies + capture any redirect
let r = await req(`https://pan.baidu.com/s/1${surl}`);
console.log('\n1) GET /s/1' + surl, '→', r.s, r.ct, 'len', r.b.length, r.loc ? '→ ' + r.loc : '');
console.log('   cookies seeded:', [...jar.keys()].join(',') || '(none)');
const bdstoken = (r.b.match(/"bdstoken":"([0-9a-f]+)"/) || [])[1];
console.log('   bdstoken on page:', bdstoken || '(none — anonymous)');

// 2) verify the password → randsk (BDCLND). surl form for /share/verify is usually WITHOUT the "1".
for (const form of [surl, '1' + surl]) {
  const v = await req(`https://pan.baidu.com/share/verify?surl=${encodeURIComponent(form)}&web=1&clienttype=0&channel=chunlei&app_id=250528`, {
    method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({ pwd: PWD, vcode: '', vcode_str: '' }).toString(),
  });
  let j; try { j = JSON.parse(v.b); } catch { j = null; }
  console.log(`\n2) POST /share/verify surl=${form} → ${v.s} → ${j ? 'errno=' + j.errno + (j.randsk ? ' randsk=' + j.randsk.slice(0, 12) + '…' : '') : 'NON-JSON ' + v.b.slice(0, 120)}`);
  if (j && j.errno === 0 && j.randsk) {
    const randsk = j.randsk;
    jar.set('BDCLND', encodeURIComponent(randsk));
    // 3a) re-GET the share page (now unlocked) — the SPA embeds uk/shareid/file-list as `yunData`.
    const pg = await req(`https://pan.baidu.com/s/1${surl}`);
    console.log(`\n3a) GET /s/1${surl} [unlocked] → ${pg.s} len ${pg.b.length}`);
    const uk = (pg.b.match(/"share_uk":"?(\d+)"?/i) || pg.b.match(/SHARE_UK["':\s]+"?(\d+)/i) || [])[1];
    const shareid = (pg.b.match(/"shareid":"?(\d+)"?/i) || pg.b.match(/SHAREID["':\s]+"?(\d+)/i) || [])[1];
    const names = [...pg.b.matchAll(/"server_filename":"([^"]+)"/g)].map(m => m[1]).slice(0, 6);
    const fsids = [...pg.b.matchAll(/"fs_id":(\d+)/g)].map(m => m[1]).slice(0, 6);
    console.log(`    yunData: uk=${uk || '?'} shareid=${shareid || '?'}  files=${JSON.stringify(names)}  fs_ids=${JSON.stringify(fsids)}`);
    // 3b) /share/list with sekey + the share-page referer (the reliable JSON list once uk/shareid known).
    if (uk && shareid) {
      const l = await req(`https://pan.baidu.com/share/list?uk=${uk}&shareid=${shareid}&order=other&desc=1&showempty=0&web=1&page=1&num=100&dir=%2F&channel=chunlei&app_id=250528&clienttype=0`, { headers: { Referer: `https://pan.baidu.com/s/1${surl}` } });
      let lj; try { lj = JSON.parse(l.b); } catch { lj = null; }
      console.log(`3b) GET /share/list uk+shareid → ${l.s} → ${lj ? 'errno=' + lj.errno + ' files=' + (lj.list ? lj.list.length : '?') : 'NON-JSON ' + l.b.slice(0, 120)}`);
      if (lj && lj.list) for (const f of lj.list.slice(0, 6)) console.log(`     ${f.isdir === 1 ? '[dir] ' : ''}${f.server_filename}  size=${f.size || ''}  fs_id=${f.fs_id}  path=${f.path}`);
    }
    console.log('\n=> transfer(转存) + download need a logged-in BDUSS cookie — built from the known API, validated live by the user.');
    break;
  }
}

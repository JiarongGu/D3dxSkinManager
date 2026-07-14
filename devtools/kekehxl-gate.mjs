// kekehxl-gate.mjs — recon PAST the WordPress "Password Protected" gate on https://kekehxl.top.
// The site (可可站) is server-rendered WP behind a site-wide password gate (Ben Huson's
// "Password Protected" plugin). The gate is HTTP-level: POST the password → it sets an auth cookie →
// subsequent GETs return the real content. This maps cleanly to a remote-source adapter "gate" step
// (cookie obtained once, reused). The user provided the shared gate password for building support.
// Zero-dep (Node 24 global fetch). Password comes from argv[2] or $KEKE_PWD — never hardcoded.
// Usage: node devtools/kekehxl-gate.mjs <password> [pathToProbeAfterLogin]
import { writeFileSync, mkdirSync } from 'node:fs';
const BASE = 'https://kekehxl.top';
const UA = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36';
const CACHE = new URL('./.cache-keke/', import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, '$1');
try { mkdirSync(CACHE, { recursive: true }); } catch {}
const PWD = process.argv[2] || process.env.KEKE_PWD;
const AFTER = process.argv[3] || '/';
if (!PWD) { console.error('need password as argv[2] or $KEKE_PWD'); process.exit(1); }

const jar = new Map();
function setCookies(res) {
  const raw = res.headers.getSetCookie ? res.headers.getSetCookie() : (res.headers.get('set-cookie') ? [res.headers.get('set-cookie')] : []);
  for (const c of raw) { const [kv] = c.split(';'); const i = kv.indexOf('='); if (i > 0) jar.set(kv.slice(0, i).trim(), kv.slice(i + 1).trim()); }
}
function cookieHeader() { return [...jar].map(([k, v]) => `${k}=${v}`).join('; '); }

async function get(url, extra = {}) {
  const res = await fetch(url, { headers: { 'User-Agent': UA, 'Accept': 'text/html,*/*', Cookie: cookieHeader(), ...extra }, redirect: 'manual' });
  setCookies(res);
  return { status: res.status, loc: res.headers.get('location'), ct: res.headers.get('content-type'), body: await res.text().catch(() => '') };
}
async function post(url, form, extra = {}) {
  const body = new URLSearchParams(form).toString();
  const res = await fetch(url, {
    method: 'POST', redirect: 'manual',
    headers: { 'User-Agent': UA, 'Content-Type': 'application/x-www-form-urlencoded', Cookie: cookieHeader(), Referer: BASE + '/', ...extra },
    body,
  });
  setCookies(res);
  return { status: res.status, loc: res.headers.get('location'), ct: res.headers.get('content-type'), body: await res.text().catch(() => '') };
}

// 1) hit login page to seed wordpress_test_cookie + read the form
const loginUrl = `${BASE}/?password-protected=login&redirect_to=${encodeURIComponent(BASE + AFTER)}`;
let r = await get(loginUrl);
console.log('1) GET login  →', r.status, '| cookies:', [...jar.keys()].join(','));
// extract the <form> and its inputs
const form = (r.body.match(/<form[\s\S]*?<\/form>/i) || [])[0] || '';
const action = (form.match(/action=["']([^"']*)["']/i) || [])[1] || loginUrl;
const inputs = [...form.matchAll(/<input[^>]*name=["']([^"']+)["'][^>]*?(?:value=["']([^"']*)["'])?[^>]*>/gi)]
  .map(m => ({ name: m[1], value: m[2] || '' }));
console.log('   form action:', action);
console.log('   form inputs:', JSON.stringify(inputs));

// 2) POST the password (fill the password field, keep hidden fields)
const pwField = inputs.find(i => /pwd|password/i.test(i.name))?.name || 'password_protected_pwd';
const payload = {};
for (const i of inputs) payload[i.name] = i.value;
payload[pwField] = PWD;
if (!('password-protected' in payload)) payload['password-protected'] = 'login';
if (!('redirect_to' in payload)) payload['redirect_to'] = BASE + AFTER;
const postUrl = action && action.startsWith('http') ? action : loginUrl;
r = await post(postUrl, payload);
console.log('\n2) POST pwd   →', r.status, '| loc:', r.loc, '| cookies now:', [...jar.keys()].join(','));
const authCookie = [...jar.keys()].find(k => /postpass|password_protected|auth/i.test(k));
console.log('   auth cookie :', authCookie || '(NONE — gate may have failed)');

// 3) follow redirect / GET the target with the auth cookie
r = await get(BASE + AFTER);
console.log('\n3) GET', AFTER, '→', r.status, '| ct:', r.ct, '| len:', r.body.length);
const title = (r.body.match(/<title[^>]*>([\s\S]*?)<\/title>/i) || [])[1]?.trim();
console.log('   title:', title);
const gated = /password-protected=login|请输入密码|password_protected_pwd/i.test(r.body);
console.log('   still gated?', gated ? 'YES (login failed)' : 'no — got real content');
writeFileSync(CACHE + 'home.html', r.body);
console.log('   saved homepage → devtools/.cache-keke/home.html');
// unique internal link shapes
const internal = [...new Set([...r.body.matchAll(/href=["'](https:\/\/kekehxl\.top\/[^"'#]*)["']/gi)].map(m => m[1]))];
const shapes = {};
for (const u of internal) { const path = u.replace(BASE, '').replace(/\d+/g, 'N'); shapes[path] = (shapes[path] || 0) + 1; }
console.log('   internal link shapes (path with digits→N : count):');
for (const [s, c] of Object.entries(shapes).sort((a, b) => b[1] - a[1]).slice(0, 20)) console.log(`     ${c}x  ${s}`);
// signals for structure
// entry-title permalinks (WP): <h2 class="entry-title"><a href="...">, or article > a
const entryLinks = [...new Set([...r.body.matchAll(/<(?:h2|h3)[^>]*class=["'][^"']*entry-title[^"']*["'][^>]*>\s*<a[^>]+href=["']([^"']+)["']/gi)].map(m => m[1]))];
const allPostLinks = entryLinks.length ? entryLinks : [...new Set([...r.body.matchAll(/href=["'](https:\/\/kekehxl\.top\/(?:archives\/)?\d+[^"']*)["']/gi)].map(m => m[1]))];
console.log('   entry permalinks   :', JSON.stringify(allPostLinks.slice(0, 8)));

const productLinks = [...new Set([...r.body.matchAll(/href=["'](https:\/\/kekehxl\.top\/product\/[^"']+)["']/gi)].map(m => m[1]))];
console.log('   product links found:', productLinks.length);

// 4) WooCommerce Store API (public, no auth) — the clean JSON path for a WooCommerce shop
for (const ep of ['/wp-json/wc/store/v1/products?per_page=2', '/wp-json/wc/store/v1/products/categories?per_page=40']) {
  const rr = await get(BASE + ep);
  const isJson = /json/.test(rr.ct || '');
  console.log(`\n4) GET ${ep} → ${rr.status} ct=${rr.ct} len=${rr.body.length} json=${isJson}`);
  if (isJson) { try { const j = JSON.parse(rr.body);
    if (ep.includes('categories')) console.log('   wc categories:', JSON.stringify((j||[]).map(c => ({ id: c.id, name: c.name, count: c.count, parent: c.parent, slug: c.slug })).slice(0, 40)));
    else { const p = (j||[])[0]||{}; console.log('   product[0] keys:', Object.keys(p).join(',')); console.log('   product[0]:', JSON.stringify({ id: p.id, name: p.name, slug: p.slug, permalink: p.permalink, prices: p.prices, is_purchasable: p.is_purchasable, is_in_stock: p.is_in_stock, type: p.type, categories: (p.categories||[]).map(c=>c.name), images: (p.images||[]).length, short: (p.short_description||'').slice(0,120), desc: (p.description||'').replace(/<[^>]+>/g,' ').slice(0,200) })); }
  } catch (e) { console.log('   parse fail:', rr.body.slice(0, 200)); } } else console.log('   head:', rr.body.slice(0, 160));
}

// 4b) OLD wp/v2 endpoints for completeness
for (const ep of ['/wp-json/wp/v2/types']) {
  const rr = await get(BASE + ep);
  const isJson = /json/.test(rr.ct || '');
  console.log(`\n4) GET ${ep} → ${rr.status} ct=${rr.ct} len=${rr.body.length} json=${isJson}`);
  if (isJson) {
    try {
      const j = JSON.parse(rr.body);
      if (ep.includes('categories')) console.log('   categories:', JSON.stringify(j.map(c => ({ id: c.id, name: c.name, count: c.count, parent: c.parent })).slice(0, 30)));
      else console.log('   post[0] keys:', Object.keys(j[0] || {}).join(','), '\n   post[0]:', JSON.stringify({ id: j[0]?.id, slug: j[0]?.slug, title: j[0]?.title?.rendered, link: j[0]?.link, categories: j[0]?.categories }));
    } catch { console.log('   (json parse failed) head:', rr.body.slice(0, 200)); }
  } else console.log('   head:', rr.body.slice(0, 160));
}

// 5) fetch one PRODUCT → find baidu link + 提取码 (extract code) + price
const target5 = productLinks[0] || allPostLinks[0];
if (target5) {
  const rp = await get(target5);
  writeFileSync(CACHE + 'product.html', rp.body);
  console.log(`\n5) GET product ${decodeURIComponent(target5)} → ${rp.status} len=${rp.body.length} (saved product.html)`);
  const price = (rp.body.match(/woocommerce-Price-amount[^>]*>[\s\S]{0,40}?([\d.,]+)/i) || [])[1];
  const free = /免费|free|价格.{0,10}0\b/i.test(rp.body);
  console.log('   price signal:', price || '(none)', free ? '(FREE-ish words present)' : '');
  const ptitle = (rp.body.match(/<h1[^>]*>([\s\S]*?)<\/h1>/i) || [])[1]?.replace(/<[^>]+>/g, '').trim();
  console.log('   post title:', ptitle);
  const baidu = [...rp.body.matchAll(/https?:\/\/pan\.baidu\.com\/[^\s"'<>]+/gi)].map(m => m[0]);
  console.log('   baidu links:', baidu.length ? [...new Set(baidu)] : '(none)');
  const codes = [...rp.body.matchAll(/(?:提取码|密码|pwd|code)[：:\s]*([0-9a-z]{4})/gi)].map(m => m[1]);
  console.log('   extract codes (提取码):', codes.length ? [...new Set(codes)] : '(none found near label)');
  const otherHosts = [...new Set([...rp.body.matchAll(/https?:\/\/(pan\.quark\.cn|mega\.nz|www\.aliyundrive|cloud\.189|lanzou[a-z]*\.com|drive\.google)[^\s"'<>]*/gi)].map(m => m[0]))];
  console.log('   other pan hosts:', otherHosts.length ? otherHosts : '(none)');
  // the content block around the first baidu link (to see the 提取码 layout)
  if (baidu[0]) { const idx = rp.body.indexOf(baidu[0]); console.log('   --- context around baidu link ---\n', rp.body.slice(Math.max(0, idx - 260), idx + 160).replace(/<[^>]+>/g, ' ').replace(/\s+/g, ' ').trim()); }
}

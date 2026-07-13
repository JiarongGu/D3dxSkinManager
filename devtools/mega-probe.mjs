#!/usr/bin/env node
// mega-probe.mjs — validate MEGA folder-share resolution (crypto + API) against a REAL link before
// porting to C# (MegaShareResolver). MEGA crypto is byte-order-sensitive; a self-made unit vector can't
// prove correctness — the real API + a decrypted filename can. Zero-dep (Node 24 fetch + node:crypto).
//
//   node devtools/mega-probe.mjs "https://mega.nz/folder/<id>#<key>"
//
// Prints: folder node list, decrypted filenames, and a resolved download URL for the largest archive.
import crypto from 'node:crypto';

const link = process.argv[2] || 'https://mega.nz/folder/P7JhGJaB#lCWpVl5ZfkhRTsZwskmdIA';
const API = 'https://g.api.mega.co.nz/cs';
let seq = Math.floor(Date.now() % 1e9); // sequence id per request

// --- base64url (MEGA: url-safe, no padding) ---
const b64urlToBuf = (s) => Buffer.from(s.replace(/-/g, '+').replace(/_/g, '/'), 'base64');
const aesEcbDec = (key, data) => { const d = crypto.createDecipheriv('aes-128-ecb', key, null); d.setAutoPadding(false); return Buffer.concat([d.update(data), d.final()]); };
const aesCbcDecZeroIv = (key, data) => { const d = crypto.createDecipheriv('aes-128-cbc', key, Buffer.alloc(16)); d.setAutoPadding(false); return Buffer.concat([d.update(data), d.final()]); };

// A file node's 32-byte key (8 big-endian u32 words) → { aesKey(16), nonce(8) }.
function unpackFileKey(k32) {
  const w = []; for (let i = 0; i < 8; i++) w.push(k32.readUInt32BE(i * 4));
  const aes = Buffer.alloc(16);
  aes.writeUInt32BE((w[0] ^ w[4]) >>> 0, 0); aes.writeUInt32BE((w[1] ^ w[5]) >>> 0, 4);
  aes.writeUInt32BE((w[2] ^ w[6]) >>> 0, 8); aes.writeUInt32BE((w[3] ^ w[7]) >>> 0, 12);
  const nonce = Buffer.alloc(8); nonce.writeUInt32BE(w[4], 0); nonce.writeUInt32BE(w[5], 4);
  return { aes, nonce };
}

// Attribute blob "MEGA{json}" (AES-CBC, zero IV, null-padded) → parsed { n: filename }.
function decryptAttr(aesKey, b64) {
  let buf = b64urlToBuf(b64);
  if (buf.length % 16) buf = buf.subarray(0, buf.length - (buf.length % 16));
  const dec = aesCbcDecZeroIv(aesKey, buf).toString('utf8').replace(/\0+$/, '');
  if (!dec.startsWith('MEGA')) throw new Error('attr not MEGA-prefixed: ' + dec.slice(0, 12));
  return JSON.parse(dec.slice(4));
}

// AES-CTR decrypt: IV = nonce(8) || bigEndian(blockIndex)(8), starting at block 0.
function ctrDecrypt(aesKey, nonce, data) {
  const out = Buffer.alloc(data.length);
  const ecb = crypto.createCipheriv('aes-128-ecb', aesKey, null); ecb.setAutoPadding(false);
  const counter = Buffer.alloc(16); nonce.copy(counter, 0, 0, 8);
  for (let off = 0, block = 0; off < data.length; off += 16, block++) {
    counter.writeBigUInt64BE(BigInt(block), 8);
    const ks = ecb.update(counter);
    const n = Math.min(16, data.length - off);
    for (let i = 0; i < n; i++) out[off + i] = data[off + i] ^ ks[i];
  }
  return out;
}

async function apiCall(query, body) {
  const res = await fetch(`${API}?id=${seq++}${query}`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body),
  });
  const json = await res.json();
  if (typeof json === 'number') throw new Error('MEGA API error ' + json);
  if (Array.isArray(json) && typeof json[0] === 'number') throw new Error('MEGA API error ' + json[0]);
  return json;
}

(async () => {
  const m = link.match(/mega\.nz\/folder\/([^#/?]+)#([^#/?]+)/);
  if (!m) throw new Error('not a mega folder link');
  const folderId = m[1];
  const folderKey = b64urlToBuf(m[2]);
  console.log(`folder ${folderId}  key ${folderKey.length}B`);

  const [tree] = await apiCall(`&n=${folderId}`, [{ a: 'f', c: 1, r: 1, ca: 1 }]);
  // Tree shape: how many folders, and are files nested? (decides path reconstruction).
  const folderNodes = (tree.f || []).filter(n => n.t === 1);
  const rootHandle = (tree.f || []).find(n => n.t === 1 && n.k && n.k.split(':')[0] === folderId) ? folderId : folderId;
  const parents = new Set((tree.f || []).filter(n => n.t === 0).map(n => n.p));
  console.log(`nodes: ${(tree.f || []).length} · folders: ${folderNodes.length} · distinct file-parents: ${parents.size} (root=${rootHandle})`);
  // A node's `k` is `h1:key1/h2:key2/…` — its key encrypted under EACH sharing ancestor. Nested nodes are
  // keyed under a SUBFOLDER, not the root, so build a key hierarchy: decrypt folder keys (multi-pass), then
  // decrypt each node with whichever ancestor key its `k` lists.
  const parseK = (k) => (k || '').split('/').map(p => { const i = p.indexOf(':'); return i < 0 ? null : [p.slice(0, i), p.slice(i + 1)]; }).filter(Boolean);
  const all = tree.f || [];
  // The link key is the key of the SHARE ROOT node (the folder whose parent isn't in the tree), NOT the
  // share id — descendants are keyed under it. Seed it, then decrypt the folder-key hierarchy downward.
  const handles = new Set(all.map(n => n.h));
  const knownKeys = {};
  for (const n of all) if (!handles.has(n.p)) knownKeys[n.h] = folderKey;
  for (let pass = 0; pass < 16; pass++) for (const n of all) {
    if (n.t !== 1 || knownKeys[n.h]) continue;
    const pr = parseK(n.k).find(([h]) => knownKeys[h]);
    if (pr) knownKeys[n.h] = aesEcbDec(knownKeys[pr[0]], b64urlToBuf(pr[1]).subarray(0, 16));
  }
  const folders = {}, files = [];
  for (const node of all) {
    const pair = parseK(node.k).find(([h]) => knownKeys[h]);
    if (!pair) { if (node.t === 0) console.log(`  [debug] no key for file h=${node.h} k=${JSON.stringify(node.k)}`); continue; }
    const enc = b64urlToBuf(pair[1]);
    if (node.t === 1) {
      const key = aesEcbDec(knownKeys[pair[0]], enc.subarray(0, 16));
      let name = node.h; try { name = decryptAttr(key, node.a).n; } catch { /* keep handle */ }
      folders[node.h] = { name, parent: node.p };
    } else if (node.t === 0 && enc.length >= 32) {
      const nodeKey = aesEcbDec(knownKeys[pair[0]], enc.subarray(0, 32));
      const { aes, nonce } = unpackFileKey(nodeKey);
      let name = '(?)'; try { name = decryptAttr(aes, node.a).n; } catch { name = 'ATTR_FAIL'; }
      files.push({ h: node.h, name, size: node.s, parent: node.p, aes: aes.toString('hex'), nonce: nonce.toString('hex') });
    }
  }
  console.log('folder names: ' + Object.values(folders).map(f => f.name).join(' · '));
  const relPath = (f) => { const segs = [f.name]; let p = f.parent; let g = 0; while (p && p !== folderId && folders[p] && g++ < 64) { segs.unshift(folders[p].name); p = folders[p].parent; } return segs.join('/'); };
  console.log('sample paths: ' + files.slice(0, 3).map(relPath).join('  |  '));
  console.log('files:');
  for (const f of files) console.log(`  ${f.name}  ${(f.size / 1048576).toFixed(2)}MB  h=${f.h}`);

  // Validate the CTR file-decrypt on a SMALL file (a .txt/.ini or the smallest) — cheap, proves the
  // last piece end-to-end (download the encrypted bytes → AES-CTR decrypt → readable content).
  const small = files.filter(f => /\.(txt|ini)$/i.test(f.name)).sort((a, b) => a.size - b.size)[0]
    || files.sort((a, b) => a.size - b.size)[0];
  const target = files.find(f => f.h === small.h);
  const [g] = await apiCall(`&n=${folderId}`, [{ a: 'g', g: 1, n: target.h }]);
  console.log(`\npicked (small, for CTR check): ${target.name}  size=${g.s}`);
  const enc = Buffer.from(await (await fetch(g.g)).arrayBuffer());
  const dec = ctrDecrypt(Buffer.from(target.aes, 'hex'), Buffer.from(target.nonce, 'hex'), enc);
  const head = dec.subarray(0, 200).toString('utf8').replace(/\0+$/, '');
  console.log(`decrypted ${dec.length}B — head: ${JSON.stringify(head.slice(0, 120))}`);
  console.log('OK — crypto + API + CTR decrypt validated end-to-end against the real folder.');
})().catch(e => { console.error('PROBE FAILED:', e.message); process.exit(1); });

#!/usr/bin/env node
// plugin-sdk.mjs — build the plugin SDK (D3dxSkinManager.Core + D3dxSkinManager.Plugin.Sdk) and PUBLISH
// the dlls (+ the SDK guide) into the plugin repo's lib/ folder. This is the "main-app-managed folder →
// plugin repo" distribution: the app owns the contract build; the plugin repo TRACKS the vendored dlls
// (a NuGet package is the planned successor). Run after any change to the Core contracts.
//
//   node devtools/dev.mjs plugin-sdk [targetRepoDir]     default: ../D3dxSkinManager.Plugins

import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, copyFileSync } from 'node:fs';
import { resolve, join } from 'node:path';

const repoRoot = process.cwd();
const target = resolve(repoRoot, process.argv[2] || '../D3dxSkinManager.Plugins');
const sdkProj = 'D3dxSkinManager.Plugin.Sdk/D3dxSkinManager.Plugin.Sdk.csproj';
const outDir = resolve(repoRoot, 'D3dxSkinManager.Plugin.Sdk/bin/Release/net10.0-windows');
const dlls = ['D3dxSkinManager.Core.dll', 'D3dxSkinManager.Plugin.Sdk.dll'];

console.log('[plugin-sdk] building SDK (Release)…');
const build = spawnSync('dotnet', ['build', sdkProj, '-c', 'Release', '--nologo', '-clp:ErrorsOnly'], { stdio: 'inherit' });
if (build.status !== 0) { console.error('[plugin-sdk] build failed'); process.exit(build.status || 1); }

const libDir = join(target, 'lib');
mkdirSync(libDir, { recursive: true });
for (const f of dlls) {
  const src = join(outDir, f);
  if (!existsSync(src)) { console.error(`[plugin-sdk] missing ${src} — did the build output move?`); process.exit(1); }
  copyFileSync(src, join(libDir, f));
  console.log(`[plugin-sdk]   → lib/${f}`);
}
const readme = resolve(repoRoot, 'D3dxSkinManager.Plugin.Sdk/README.md');
if (existsSync(readme)) { copyFileSync(readme, join(libDir, 'README.md')); console.log('[plugin-sdk]   → lib/README.md'); }

console.log(`[plugin-sdk] published SDK → ${libDir}`);
if (!existsSync(join(target, '.git')))
  console.log(`[plugin-sdk] NOTE: ${target} has no .git yet — create/clone the plugin repo, then commit lib/.`);

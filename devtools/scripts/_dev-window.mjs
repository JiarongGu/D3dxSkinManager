// _dev-window.mjs — resolve the DEV instance's main-window HWND by exe PATH, never by process name.
// The user often runs their own INSTALLED copy of the app alongside the dev instance; a bare
// Process.GetProcessesByName match can target THEIR window (it captured the user's window and
// `taskkill /IM` killed their instance, both 2026-07-05). Path-matching the repo bin exe guarantees
// every native tool (shot / input / kill) only ever touches the dev instance.
import { execSync } from 'node:child_process';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import cfg from '../project.config.mjs';

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');

/** HWND (hex string like "0x51a2c4") of the dev instance's main window, or null when not running. */
export function devMainWindowHwnd() {
  const exePath = resolve(repoRoot, cfg.exe);
  try {
    const out = execSync(
      `powershell -NoProfile -Command "(Get-Process ${cfg.processName} -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq '${exePath.replace(/'/g, "''")}' -and $_.MainWindowHandle -ne 0 } | Select-Object -First 1).MainWindowHandle"`,
      { stdio: 'pipe' },
    ).toString().trim();
    const n = Number(out);
    return Number.isFinite(n) && n > 0 ? '0x' + n.toString(16) : null;
  } catch { return null; }
}

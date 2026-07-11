# Screenshot hygiene — keep captures small + few (avoid the stuck-image API error)

The agent's image-read tool **rejects images larger than ~2000px** in either dimension. WGC window
captures can exceed that, so a raw capture read directly throws:

> API Error: an image in the conversation could not be processed and was removed.

Worse: a rejected image **stays in the conversation context and re-errors on every subsequent turn**
until the context is compacted/cleared. Oversized captures poison the session, not just one turn.
(Adapted from a sibling project — hard-won.)

## The fix (built into the capture tooling — don't bypass it)
`devtools/scripts/_capture-util.mjs` exposes `finalizeShot(file)`, called by every capture path:
- **`downscaleInPlace`** — GDI+ (PowerShell, no npm dep) shrinks the PNG so neither side exceeds
  **1600px** (no-op if already small). A saved shot is ALWAYS agent-readable.
- **`pruneShots`** — keeps only the newest **8** PNGs in `devtools/screenshots/`; deletes the rest.
  Captures are throwaway scratch (git-ignored, see [scripts-live-in-repo.md](../rules/scripts-live-in-repo.md)).

Wired into all capture paths: `shot-wgc.mjs` (`dev.mjs shot`) and `drive-cdp.mjs` (`cdp shot`, `cdp grab`).

## Rules for the agent
1. **Capture via the tooling only** (`node devtools/dev.mjs shot|cdp shot|cdp grab`) — never read a raw
   full-res PNG directly. If you must, run `node devtools/downscale.mjs <path>` first and read the copy.
2. If you ever hit the oversized-image error: it won't clear itself — tell the user to `/compact` (or
   `/clear`) and **stop reading images** until then. Delete the offending PNG from `devtools/screenshots/`.
3. Prefer **DOM/IPC text verification** (`cdp eval` / `cdp ipc` returning JSON) over a screenshot when you
   only need to confirm wiring/values — cheaper, never risks the image limit.
4. Don't commit screenshots; `devtools/screenshots/` is scratch.

`MAX_DIM` (1600) and `KEEP` (8) live at the top of `_capture-util.mjs` — adjust there, in one place.

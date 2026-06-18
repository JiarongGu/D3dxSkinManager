# DPI scaling — the app is PerMonitorV2; do NOT hand-convert px for the UI

The host sets **`Application.SetHighDpiMode(HighDpiMode.PerMonitorV2)`** (`Infrastructure/
ApplicationBootstrapper.cs`). So Windows + WebView2 scale the window and the React UI **automatically**
per-monitor. **CSS px in the React app are device-independent** — the browser engine renders them crisp
at any DPI (125% / 150% / 200% / mixed-monitor). There is **nothing to "convert" for the UI**.

## Rules
- **NEVER add px↔DPI math to the React UI or to window dimensions.** Use plain CSS px (the 12/14px font
  rule etc. are device-independent). Window sizes/positions are auto-scaled by PerMonitorV2 — manually
  scaling them **double-scales** (a real bug). `DpiHelper` even says so: *"Do NOT use for window
  dimensions — those are auto-scaled by Windows."*
- **Manual DPI math is allowed ONLY in genuinely physical-pixel native domains**, where the OS API
  works in raw device pixels regardless of awareness. The legitimate (and only) users of
  `DpiHelper.GetDpiScaleFactor()` / `ScalePixels()`:
  - **ScreenCapture** (`ScreenCaptureOverlay`, `ScreenCaptureService`) — overlay border/hit-area sizes
    and the capture region work in real screen pixels.
  - **SecondaryWindowService** — restoring saved window bounds **when the monitor DPI changed**
    (`ProfileConfiguration.SavedDpiScale` converts old physical px → new).
  - **SystemFacade** — reporting screen resolution (physical → logical).
  - **DropZoneManager** — OS drag-drop coordinates (physical → logical) for WebView2 hit-testing.
- If you find yourself reaching for `DpiHelper` anywhere else (a panel size, a font, a margin, a React
  value), **stop** — it's almost certainly already handled by PerMonitorV2 + CSS.

## Why this is settled
This was reviewed 2026-06-19: the app already does the "use DPI not px" approach the right way — the only
remaining conversions are the four native physical-pixel cases above, which are mandatory and cannot be
removed. There is no UI-side px recalculation to eliminate.

# DPI scaling — the app is PerMonitorV2; do NOT hand-convert px for the UI

The host sets **`Application.SetHighDpiMode(HighDpiMode.PerMonitorV2)`** (`Infrastructure/
ApplicationBootstrapper.cs`). So the **React UI** scales **automatically** per-monitor: **CSS px in the
React app are device-independent** — the browser engine renders them crisp at any DPI (125/150/200%,
mixed-monitor). There is **nothing to "convert" for the React UI**.

**BUT a WinForms FORM's outer size set in code is NOT auto-scaled from a logical baseline** (corrected
2026-07-12). WinForms window coordinates are **device px at the form's current DPI**, and a
`Form.Width`/`Height` you set in code (before the handle exists, no `AutoScaleMode`/`AutoScaleDimensions`)
stays device px. So a persisted/default size stored in LOGICAL px must be **× the current monitor DPI**
when applied — otherwise a 1280×800 default becomes 1280×800 *device* px → an ~853×533-logical (tiny)
window on 150%. This was a real bug in the MAIN window (fixed 2026-07-12, see `WindowStateService`).

## Rules
- **React UI: NEVER add px↔DPI math** (CSS px are device-independent; the 12/14px font rule etc.).
- **WinForms window size/position: store LOGICAL px; × the current monitor DPI when applying it to the
  form, ÷ DPI when saving.** This is NOT "double-scaling" — WinForms does not auto-scale a code-set form
  size, so the conversion is REQUIRED. Keep the DPI an **in-memory, per-start** concern — never persist
  the scale (each launch can be a different monitor DPI); persist only the logical px. `DpiHelper`'s "do
  NOT use for window dimensions" note means *don't re-scale a value WinForms already scaled* (control
  layout) — it does NOT mean "skip the logical→physical form-size conversion".
- **Manual DPI math is allowed ONLY in genuinely physical-pixel native domains**, where the OS API
  works in raw device pixels regardless of awareness. The legitimate (and only) users of
  `DpiHelper.GetDpiScaleFactor()` / `ScalePixels()`:
  - **ScreenCapture** (`ScreenCaptureOverlay`, `ScreenCaptureService`) — overlay border/hit-area sizes
    and the capture region work in real screen pixels.
  - **SecondaryWindowService** — restoring saved secondary-window bounds across a DPI change
    (`ProfileConfiguration.SavedDpiScale` converts old physical px → new).
  - **WindowStateService** (MAIN window, added 2026-07-12) — `ToPhysicalState` converts the persisted
    LOGICAL size × the current DPI on load, and ÷ `form.DeviceDpi` on save. Same domain as
    SecondaryWindowService. Tests: `WindowStateServiceTests`.
  - **SystemFacade** — reporting screen resolution (physical → logical).
  - **DropZoneManager** — OS drag-drop coordinates (physical → logical) for WebView2 hit-testing.
- If you find yourself reaching for `DpiHelper` anywhere else (a panel size, a font, a margin, a React
  value), **stop** — it's almost certainly already handled by PerMonitorV2 + CSS.

## Why this is settled
Reviewed 2026-06-19 (React-UI side: use DPI not px — nothing to eliminate) and corrected 2026-07-12
(the WinForms form-size side: a code-set form size IS device px and needs the logical→physical
conversion — the main window was missing it and started tiny on high-DPI). The listed DpiHelper users
are the only legitimate ones; don't add px↔DPI math to React or to already-scaled control layout.

# WebView2 resource serving (`app://` + `app.local`) — DEFER off the UI thread, never DECODE in the path

`WebViewInitializer` registers `CoreWebView2.WebResourceRequested` to serve two schemes:
- `app://…` → on-disk dynamic files (mod previews, category/profile thumbnails) via `CustomSchemeHandler`.
- `https://app.local/…` → the embedded prod bundle (JS/CSS/html) via `IEmbeddedResourceProvider`.

`WebResourceRequested` fires on the **WinForms UI thread**. Two independent things must both be true,
and getting either wrong has bitten us (multiple sessions):

## 1. Serve OFF the UI thread (deferral) — serving synchronously FREEZES the window
If you resolve the response **inline** (read the file + `CreateWebResourceResponse` in the handler body),
the UI thread is blocked for the duration of **every** request. A library with hundreds of category
cards fires that many requests in a startup/scroll burst → the window **freezes** (no paint, no input)
while they serialize. **Do NOT serve synchronously.** Call `args.GetDeferral()` so the handler returns to
the UI thread immediately (microseconds); do the read on the thread pool and create the response back on
the UI thread (CoreWebView2 is UI-affine) via **non-blocking `BeginInvoke`** (never blocking `Invoke` —
that can deadlock, see `DropZoneManager`). `deferral.Complete()` in a `finally`.

## 2. Never DECODE an image in the request path — that, not the deferral, was the "slow thumbnails" bug
A serve-time "downscale thumbnails" step (`ImageHelper.GetOrCreateDownscaled` from `CustomSchemeHandler`)
ran a full ImageSharp `Load<Rgba32>` on **every** request — and for an image already ≤ the bound it
returned the source *without caching*, so the cache never hit and **every request re-decoded the image
just to measure it**. Thumbnails are already ~250 px (pre-sized at import — `category.thumbnail`,
mod previews), so this was pure wasted CPU. CPU-bound decode tasks also pile up faster than the thread
pool ramps (~1 thread/250 ms) → the burst tail waits seconds. **Removed.** `GetOrCreateDownscaled` is left
in `ImageHelper` as an unused utility — do NOT wire it back into serving. Chromium decodes images
hardware-accelerated in the renderer; just stream the original bytes.

## Use async I/O for the read (kills any thread-pool-ramp risk)
`CustomSchemeHandler.HandleRequestBytesAsync(url)` reads with `File.ReadAllBytesAsync` (no thread held
during the I/O), so even a 2000-request burst doesn't stall on pool ramp-up. The sync `HandleRequest`
(stream) is kept for any non-deferred caller; both share `ResolveRequest` (path resolve + content type).

## The correct shape (current — verified 2026-06-19)
- Deferral + `Task.Run(async …)` → `await HandleRequestBytesAsync` (app://) / read embedded stream
  (app.local) → `BeginInvoke` builds `CreateWebResourceResponse` on the UI thread → `deferral.Complete()`.
- **Keep the `Cache-Control` headers** — `public, max-age=86400` for `app://`, `…, immutable` for the
  bundle. WebView2 serves repeats from its own cache without re-entering the handler (cross-launch
  first-paint win). `app://` callers cache-bust with `?t=<mtime>`.
- Thumbnails are downscaled **at import time**, not at serve time.

## Measured proof (deferral + async + no decode)
Burst test (cache-busted app:// requests so each hits the handler): **300 images → 180 ms (0.6 ms each),
0 frames > 50 ms**; 2000 images → 1025 ms with only minor renderer-decode jank (real grids virtualize and
never request that many at once). Normal startup: 39/39 thumbnails, window composites fully (native WGC
capture). Contrast: the synchronous version froze the UI thread on big libraries.

## Why this keeps biting
The two failure modes look like opposites, so fixing one reintroduces the other:
- "Make it concurrent" → deferral, but people also add a decode → slow.
- "The deferral is slow" (really the decode) → rip out the deferral → synchronous → UI freeze.
The right answer is BOTH: **defer (never block the UI thread) AND never decode (stream raw bytes)**.
Measure before changing — a sub-ms local read does not need synchronous serving, and an already-small
image does not need decoding.

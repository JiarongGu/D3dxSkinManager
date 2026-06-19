# WebView2 resource serving (`app://` + `app.local`) — serve SYNCHRONOUSLY, never decode in the request path

`WebViewInitializer` registers `CoreWebView2.WebResourceRequested` to serve two schemes:
- `app://…` → on-disk dynamic files (mod previews, category/profile thumbnails) via `CustomSchemeHandler.HandleRequest`.
- `https://app.local/…` → the embedded prod bundle (JS/CSS/html) via `IEmbeddedResourceProvider`.

These are **all local reads** (in-memory embedded resources + small on-disk PNGs). A local read is
sub-millisecond. **Set `args.Response` synchronously, inline, on the UI thread.** That is the fast path
and matches the fast pre-2.5 behaviour.

## Two regressions that made local serving "slower than a real web server" — do NOT reintroduce

1. **Deferral + `Task.Run` per request (the "serve concurrently like a web server" idea).** Looks right,
   is wrong for many tiny local files: the cold thread pool ramps ~1 thread / 250 ms, so a startup burst
   (the mod-grid = ~40 thumbnails requested at once) queues and the *tail* waits **seconds**; every
   response also double-marshals back to the UI thread (`BeginInvoke`) to call the UI-thread-affine
   `CreateWebResourceResponse`. Net: slower than synchronous. **Local file reads do not need offloading.**
   (Offloading is for genuinely slow/blocking work — there is none here.)

2. **Decoding images in the request path.** A serve-time "downscale thumbnails" step
   (`ImageHelper.GetOrCreateDownscaled` called from `CustomSchemeHandler`) ran a full ImageSharp
   `Load<Rgba32>` on **every** request — and for an image already ≤ the bound it returned the source
   *without caching*, so the cache check never hit and **every request re-decoded the image just to
   measure it**. Thumbnails are already ~250 px (pre-sized at import), so this was pure wasted CPU on top
   of a raw byte read. **Don't decode/resize in the handler.** Chromium's renderer decodes images
   hardware-accelerated; just stream the original bytes. (Removed 2026-06-19; `GetOrCreateDownscaled`
   left in `ImageHelper` as an unused utility — do not wire it back into serving.)

## The correct shape (current)
- Synchronous handler: `args.Response = env.CreateWebResourceResponse(stream, 200, "OK", headers)` for
  both schemes; 404 stream on miss; try/catch sets a 404 (webview may be tearing down).
- **Keep the `Cache-Control` headers** — `public, max-age=86400` for `app://`, `…, immutable` for the
  bundle. They let WebView2 serve repeats from its own cache **without re-entering the handler**, which is
  the real cross-launch first-paint win (cheap, safe, keep it). `app://` callers cache-bust with `?t=<mtime>`.
- Previews/thumbnails are downscaled **at import time**, not at serve time. If a source is ever large,
  let the renderer decode it (fast) rather than CPU-decoding on the .NET side.

## Why this keeps biting
"Make it concurrent / async" is the instinct, but the bottleneck was never UI-thread blocking on a fast
local read — it was the work we *added* (thread-pool ramp + per-request decode). Measure before
offloading: a sub-ms local read offloaded to a cold pool is slower, not faster. Verified 2026-06-19:
39/39 thumbnails load, DOMContentLoaded ~480 ms, no per-request decode.

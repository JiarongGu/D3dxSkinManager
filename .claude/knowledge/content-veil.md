# Content veil — sensitive-preview blur (pure-CPU point-detection algorithm, opt-in global toggle)

Shipped + redesigned 2026-07-10 across a long live-review loop with the user. "Content veil" is the
feature vocabulary everywhere — service, IPC, setting, verdicts, UI copy — not "NSFW".

## Hard-won product decisions (do NOT re-litigate without the user)

- **The IMAGE alone decides — explicit only.** The user's bar: nudity veils; suggestive outfits
  (bunny suits, swimsuits, "bottom heavy" skins, leotards) do NOT.
- **ML is OPT-IN via the PLUGIN system — never in the base bundle.** Two bundled-ONNX attempts
  were measured and removed (a general classifier scored suggestive==explicit at p=0.97; an
  anime-rating classifier broke on 3D renders — a mecha at r18=0.99; +12-28MB bundle each; the
  publish stays ~17MB). The shipped answer is the **content-veil AI plugin pack**
  (`plugins/D3dxSkinManager.Plugins.ContentVeil` — deepghs anime CENSOR-POINT detector, MIT,
  v1.0_n nano 11.5MB embedded in the plugin dll): a part DETECTOR matches the explicit-only bar
  exactly. A/B'd nano vs s (42.5MB) on the corpus 2026-07-11: nano WON (95.8% vs 94.4% acc,
  1.8× faster, ¼ size). Downloadable in-app (Settings → 插件); see `plugin-system.md`.
- **NO title/keyword detection**, and the SITE's content rating does NOT force a veil (GB rates
  whole MODS; rated mods often have tame thumbnails). The `Sensitive` flag is still captured per
  index entry (engine → `RemoteIndexEntries.Sensitive`, migration 202607100001) as DATA only.
- **Veil surface = the REMOTE library grid ONLY** (the mod-detail carousel was de-veiled; detail
  screens/viewers stay unveiled).
- **Some misses are accepted** ("really small and fast tech — we can allow some level"): animated
  UI-panel collages with small explicit imagery evade local features.

## The algorithm (ContentVeilService — zero deps, a few ms per image, grid 256px)

1. **Skin mask** — classic per-pixel rules (RGB Kovac ∪ YCbCr chroma box).
2. **Body-shape stage** — 4-connected skin regions + per-region stats/bboxes; border-dominant
   (≥25% of frame border) tonally-flat (luma σ ≤26) regions rejected as BACKDROPS; speckle ignored.
   Features: fgSkinRatio, largestFgRegion (+contiguity = largest/fg), bgSkinRatio, regionCount.
3. **Explicit-point stage** — two sources:
   - **HOLE points**: small compact non-skin blobs ≥70% enclosed by one body region, redder
     (ΔCr) and not brighter than it. Rare and strong — the verdict count caps apply to these.
   - **IN-REGION anomaly points**: redder+darker patches whose pixels still pass the skin rules
     (subtle areolas on soft renders). Noisy (blush/lips) → they ONLY participate in the ZOOM
     pass's pair evidence, under stricter geometry + per-region top-N-by-score.
4. **ZOOM pass** — when pass 1 finds ONE dominant small-in-frame body (largestFg 0.03-0.35,
   contiguity ≥0.7), crop its bbox (+10%) from the ORIGINAL and re-run stages 1-3 at body scale;
   the crop's point evidence REPLACES pass-1 point rules (a standing figure's nipples are 1-2px on
   the pass-1 grid; and a menu panel that collides with a nude signature at pass 1 gets re-judged).
5. **Verdict** (`PointEvidence`; every hit records `VerdictRule` — tuning telemetry):
   - PAIR of similar points, one horizontal band, same region, few points total (decorated outfits
     produce many; cap 5 pass-1 / 6 zoom), pair's body ≥0.10 of frame, both members score ≥0.5.
   - Exposed-body: 3-5 strong (≥0.9) hole points on a big region (0.50-0.90 — >0.9 = texture SHEET).
   - Mass exposure (bare skin amount): **DISABLED by default** — the 2026-07-11 sweep measured 0
     TPs / only FPs from it. Kept as a sweepable knob (`MassExposureMinFg`, 2.0 = off).

## The AI plugin as INTERCEPTOR (when installed)

The CV pipeline always runs; `IImageReviewPlugin` reviewers then intercept with the context
(path + the CV pass's FOCUS REGIONS + current verdict). A reviewer's confidence REPLACES the CV
verdict (measured: CV rules add only FPs on top of the detector); abstain (null) → CV verdict
stands — so no-plugin installs keep the pure-CV behavior below. The plugin does region-TTA
internally (ambiguous full-image conf → re-detect on focus regions at full res). Threshold
`PluginMinConfidence` = 0.6 (swept 2026-07-11: negatives 97.6%, recall 93.5%, acc 95.8% on the
72-image corpus; residual misses = already-mosaic'd art the detector is blind to by design).

## The tuning loop (built for CONTINUOUS iteration — this is the durable part)

- **Labeled corpus** = `devtools/fixtures/veil/positive|negative/` (LOCAL, untracked — folder is
  the label; drop images in by hand). `devtools/fixtures/veil-labels.json` maps index titles →
  auto-snapshot (first `veil labels` run downloads each into the right folder, real extension).
- **`node devtools/dev.mjs veil labels`** — evaluate the corpus (confusion + per-file mismatch
  features incl. zoom).
- **`node devtools/dev.mjs veil sweep`** — GRID-SEARCH `ContentVeilTuning` (in-region ΔCr/darker
  margin, pair min score, top-N, zoom pair cap) via per-request overrides on
  `CONTENT_VEIL_INSPECT` `{urls, tuning}` — no rebuild per config; tuned calls bypass the cache.
  Apply the winner to `ContentVeilTuning` defaults, rebuild once.
- **`node devtools/dev.mjs veil [pages] [gameId]`** — GB Subfeed ratings as WEAK labels (judge FPs
  strictly, FNs loosely — ratings cover the MOD, not the thumbnail).
- `ContentVeilServiceTests.LabeledImageCorpus_…` runs the real function over the negative folder
  (no-op when absent) with a 25% FP regression ceiling — the exact operating point is the sweep's
  job, the test only catches code regressions.
- Corpus state 2026-07-11 (72 images, 31 positive): CV-only = TP 7 / FP 1 (precision-first
  fallback); WITH the AI plugin = acc 95.8%, negatives 97.6%, recall 93.5% (TP 29 / FP 1 / FN 2).
  Target spec: positives → 100%, negatives → >95%. Keep growing the corpus; re-sweep; move defaults.
- SPEED (2026-07-11): `InspectAsync` analyzes batches in PARALLEL (SemaphoreSlim, ≤ cores-1 capped
  8) and decodes capped at 1024px (`DecoderOptions.TargetSize` — JPEG IDCT scaling; the zoom pass
  crops from that, still 4× grid detail). Fresh 61-image corpus run ≈ 2s wall incl. IPC.

## The wiring chain (copy for new veil surfaces)

```
ContentVeilService (Core singleton)          2-pass pipeline + (path,mtime) metrics cache
  → SystemFacade "CONTENT_VEIL_CHECK"        batch { urls } → { verdicts: { url: verdict } }
  → SystemFacade "CONTENT_VEIL_INSPECT"      + metrics; optional { tuning } override (sweep)
    → systemService.checkContentVeil(urls)
      → useContentVeil.ts                    useContentVeilEnabled / useContentVeilVerdicts / isVeiled
        → <ContentVeil veiled badge>         L1 atom: pure-CSS filter:blur veil + badge, :hover reveals
RemoteLibraryView: every card goes through the verdict batch (image-only decision).
```

- **Verdicts are STRINGS `"sensitive" | "safe" | "unknown"`** (not an enum — no camelCase trap).
- **Veil-until-verdict:** pending → veiled (no flash); `safe` AND `unknown` reveal. A failed IPC
  marks urls `unknown` frontend-side.
- **Never analyze at serve time** (`webview-resource-serving.md`) — the check is a separate IPC
  AFTER images render; it resolves the SAME urls the `<img>` uses (`app://<enc>?t=` stripped,
  `proxy://image/?u=<enc>` via the remote-image cache, bare local paths defensively — the harness
  uses bare fixture paths).
- **Setting:** `GlobalSettings.ContentVeilEnabled` (default OFF), key `contentVeilEnabled`,
  Settings → 全局设置 → 隐私; op `updateContentVeilEnabled`.
- **Caching:** backend `(path, mtime)` → metrics map (bypassed for tuned calls); frontend
  module-level url→verdict map per session. Threshold changes need rebuild + UI reload.

Tests: `ContentVeilServiceTests` (paired-points vs plain-mass, zoom-pass catch, texture-sheet
guard, mass exposure, backdrop rejection, scattered, unknown, app-url, mtime cache, metrics,
labeled-corpus regression ceiling), `GameBananaEngineTests` (visibility → Sensitive capture).

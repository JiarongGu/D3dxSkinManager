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
  (pack `content-veil-ai`, in the separate plugin repo — deepghs anime CENSOR-POINT detector, MIT,
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

## Code shape (extracted 2026-07-12 — service = orchestration, analyzer = engine, verifiers = styles)

- **`ContentVeilService`** = ORCHESTRATION only: url resolve, the `(path,mtime,reviewer)` verdict cache,
  batch parallelism, the plugin INTERCEPTOR chain. No image code.
- **`IContentVeilAnalyzer` / `ContentVeilAnalyzer`** = the standalone detection ENGINE: decode → grid →
  stages 1-2, then runs an ordered list of verification STYLES and combines them (first Sensitive wins;
  an authoritative Safe stops later styles; NotApplicable falls through → coverage is the UNION).
- **`VeilVision`** (static) = the pure vision primitives (skin mask, region labeling, point detection,
  pairing, zoom crops) shared by the styles. **`VeilFrame`** = one decoded image handed to a style.
- **`IContentVerifier`** = one verification STYLE (`Order`, `Verify(frame)→VerifyResult`). Registered in
  Core DI as an `IEnumerable` (both survive — `AddSingleton` not TryAdd). Shipped styles:
  - **`PointAnatomyVerifier`** (Order 0, primary) — the pass-1 point/exposedBody/mass rule + the
    dominant-body & multi-region collage zoom passes (the dominant-body zoom REPLACES pass-1 and is
    authoritative). Reproduces the original inline verdict EXACTLY.
  - **`ChestBandZoomVerifier`** (Order 100, secondary) — see the corpus-state note below.
  - **Adding a new style = a new `IContentVerifier` + one DI line.** This is the "more range of
    verification" seam — sweep it with the harness, ship or park it via its tuning knob.

## The algorithm (VeilVision — zero deps, a few ms per image, grid 256px)

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
   - Exposed-body: 2-5 hole points, score ≥0.60, on a body region 0.12-0.90 (loosened for recall
     2026-07-12; the chest-band gate below is what keeps the FP down at these looser bars).
   - Mass exposure (bare skin amount): **DISABLED by default** — the 2026-07-11 sweep measured 0
     TPs / only FPs from it. Kept as a sweepable knob (`MassExposureMinFg`, 2.0 = off).
6. **Anatomical chest-band gate** (`InChestBand`, 2026-07-12, from the classical "nipple = shape + color
   at the ideal body PROPORTION" literature). An areola point counts only if its vertical position within
   its body region's bbox is in `[ChestBandTop 0.08, ChestBandBottom 1.0]` — i.e. NOT the top 8%
   (head/hair/lips). The raw point signal fires as strongly on negatives as positives (navels, decor,
   lips = redder compact blobs); the TOP gate rejects the head/lip class, which removed ~3 FP and let the
   exposed-body/point thresholds loosen for recall with no FP cost. A BOTTOM gate HURT (a full-figure
   region's chest sits low in the bbox), so it stays open at 1.0. Net: CV-only 32%→38% recall at 78%→83%
   negatives.

## The AI plugin as INTERCEPTOR (when installed)

The CV pipeline always runs; `IImageReviewPlugin` reviewers then intercept with the context
(path + the CV pass's FOCUS REGIONS + current verdict). **Contract v2 (2026-07-13): a reviewer returns a
bool VERDICT** (`true`=sensitive / `false`=safe / `null`=abstain) and OWNS its own threshold — the host
holds NO cutoff (`PluginContract.Version` = "2.0"; packs on 1.x are gated out until rebuilt). A verdict
REPLACES the CV verdict (measured: CV rules add only FPs on top of the detector); abstain (`null`) → CV
verdict stands, so no-plugin installs keep the pure-CV behavior below. Any reviewer's SENSITIVE verdict
wins across the chain. The plugin does region-TTA internally (ambiguous full-image conf → re-detect on
focus regions at full res), then thresholds.

**Tuning lives IN the plugin, not the host.** The ContentVeil pack's `SensitivityThreshold` = **0.40**
(recall-first, 2026-07-13): nano → recall 95.7% / neg 86% on the 90-image corpus (47 pos). Target recall
95+ / neg 90+ is **NOT jointly reachable** — the corpus OVERLAPS (1 positive undetectable @ai 0.11, hard
positives @0.47-0.54, suggestive-safe negatives @0.54-0.68); @ 0.50 = 89.4% / 90.7%. **A/B'd deepghs `s`**
(54MB, 2.4× slower @152ms vs nano ~64ms): only ~+2pts neg at matched recall — not worth it; no better small
explicit-point model exists (alternatives are censorship-*taggers* or the rejected suggestive==explicit
*classifiers*). **The ceiling is the CORPUS, not the model** — push both targets only via corpus work
(relabel/trim borderline negatives; accept the undetectable positive). To re-sweep/retrain, work in the
PLUGIN repo against its corpus. The host's `veil labels` now only reports the served VERDICT + the
reviewed recall/neg (no confidence to sweep — the plugin owns the cutoff).

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
- Corpus state 2026-07-12 (76 images, 34 positive). **WITH the AI plugin @ PluginMinConfidence 0.60
  (default) = recall 100% / negatives 95% (TP 34, FN 0, FP 2)** — meets the "≥99% positive / ≥95%
  negative" bar; the 2 FP are borderline plugin calls (ai 0.60/0.63 on tiny images). Sweep 2026-07-12
  confirmed 0.60 is the knee: 0.55-0.60 hold 100% recall, 0.65 drops to 88% (positives cluster in
  0.60-0.65). Do NOT lower (adds FP) or raise (kills recall). **Cold-start race (FIXED 2026-07-12):** the veil check
  can run before the ~24MB ONNX model finishes loading at profile init, so an early card would get a
  CV-fallback verdict — the metrics cache key now includes reviewer-presence (`|p` vs `|c`), so once the
  plugin is ready the plugin RE-DECIDES and the stale CV entry is never served. Steady-state = 100/95
  above. **CV-only (no plugin) is tuned RECALL-first** (user directive 2026-07-12: cover
  positives first, minimize FP second): the `exposedBody` rule was loosened (MinPoints 3→2, MinScore
  0.90→0.60, MinRegion 0.50→0.12), the **anatomical chest-band gate** added, AND the secondary
  **`ChestBandZoomVerifier`** style → **recall 23.5%→38%→41% at 83% negatives** (TP 8→13→14, FP 7/42).
  The `chestZoom` style covers a class the primary structurally CANNOT reach: a body that FILLS the frame
  (`big > ZoomMaxRegion 0.35`, so the whole-body zoom is skipped — no scale gain) whose areolas are
  sub-grid at frame scale (`pts=0`). It crops the CHEST BAND of the dominant region and re-scans, and
  fires **PAIR-only** — the bilateral signal recovered a real full-frame nude (`chestzoom:pair`) while the
  exposedBody rule on the same crop FP'd on a suggestive bunny-suit (skin-mass → strong points). Pair-only
  = clean +1 TP, no FP. Knobs `ChestZoomMinRegion` (0.35; ≥1 = off), `ChestZoomBandTop/Bottom` (0.08/0.55).
  ~41% is near the practical CEILING of pure CV here, far below 90% **by design of the medium**: the
  remaining ~13/34 positives carry NO detectable anatomy signal (`pts=0` + `zoom pts=0` even chest-zoomed)
  — genital-only, mosaic'd, tiny-UI-collage, or shape-IDENTICAL to swimsuit negatives
  (`POS fg=0.99 big=0.99 contig=1.0` vs a `neg fg=0.98 big=0.98 contig=1.0`). Confirmed by a 2026-07-12
  web search: classical nudity detection = skin-region % (the FP-prone mass approach); classical nipple
  detection needs face+proportion or a CNN; SOTA = CNN (= the plugin). **Measured dead-ends (do NOT
  re-attempt without new evidence):** re-enabling mass-exposure (skin AMOUNT) only adds FP; a breast-lobe
  shape+color gate trained over 60 configs moved recall by ZERO; the chest-zoom's **exposedBody** rule
  (vs pair) only added the bunny FP. What DID work: the chest-band TOP gate (reject head/lip reds) let the
  point thresholds loosen, and the PAIR-only chest-zoom style added the last recoverable full-frame nude.
  Finer separation of the remaining hard set is the anatomy detector's job.
  Harness: `node devtools/dev.mjs veil dump` prints per-image shape features (pos vs neg) to see it.
  Keep growing the corpus; the AI plugin is the recall path. Native = a best-effort no-plugin fallback.
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

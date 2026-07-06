# D3dxSkinManager — User Guide

D3dxSkinManager is a **mod library manager** for 3DMigoto‑based games (via XXMI): Genshin Impact (GIMI), Zenless Zone Zero (ZZMI), Honkai: Star Rail (SRMI), Wuthering Waves (WWMI), Honkai Impact (HIMI), Endfield (EFMI), and more.

It stores your mods **compressed**, lets you **organize, fix, edit, preview and deploy** them, and hands the actual in‑game injection to **XXMI**. Think of it as: *this app = your mod library + workshop; XXMI = the runtime that loads mods into the game.*

This guide is organized as **workflows** — follow the section that matches what you want to do. New here? Read them in order 1 → 3; that's the whole basic loop (set up → add mods → play).

> **Core idea:** mods live compressed in your library. **Loading** a mod extracts and deploys it into the game's Mods folder; **unloading** removes it. Only **one mod per category** is active at a time — loading a mod automatically unloads the previous one in the same category.

---

## 1. First‑Time Setup

**Goal:** get a profile and a deploy target so mods can load and the game can launch.

1. **Create or select a Profile** — click the **profile selector** (top‑right header). Each profile is its own workspace (mod database, categories, settings). Use one per game.
2. **Open Settings → Mod Work** and choose a **Work Directory** — where mods deploy so the game loads them:
   - **App default** — the app manages its own folder.
   - **XXMI Launcher** *(recommended)* — point at your XXMI install, then pick the importer (e.g. `ZZMI`). This sets the deploy target (that importer's `Mods` folder) **and** the launch command in one step.
   - **Custom folder** — any 3DMigoto `Mods` folder.
3. **Confirm.** When you pick an XXMI importer, a confirmation shows the work dir, deploy target, launcher path and launch args before anything is applied.

> Next: add some mods (section 2), then load and launch (section 3).

---

## 2. Add Mods to Your Library

**Goal:** get mods into your compressed library so you can organize and deploy them.

### Import a file or folder
1. **Drag & drop** an archive (`.zip` / `.7z` / `.rar`) or a mod folder onto the window — or use the **Import** button.
2. The **import workflow** handles multi‑mod archives, naming and category assignment before the mods enter your library.

### Import a shared package
- Someone sent you a **package**? Import it the same way. You can also **export** a mod (or a set) as a package to share.

> To pull mods from a website instead, see **section 8 — Download from the Remote Library**.
> Every added mod is **normalized** into the app's storage format (extracted + re‑compressed), so odd containers or passwords never break loading later.

---

## 3. Load a Mod & Launch the Game

**Goal:** activate a mod and play with it.

1. Go to the **Mods** tab. Pick a **category** on the left; its mods show on the right.
2. **Click a mod** to **load** it — the app extracts and deploys it to your work directory.
3. Remember the rule: loading a mod **unloads** any other mod in the **same category** (one active per category). Click the loaded mod again to **unload**.
4. Press **Launch** (启动) in the status bar to start the game. With XXMI, launch injects the mod DLL and opens the game with mods active.

### Save loadouts with Presets
- A **preset** stores your current set of loaded mods. Save one, then apply it later to restore exactly that combination — great for switching between curated setups. Presets live in the header / status‑bar **Presets**.

---

## 4. Organize Your Library

**Goal:** keep a large library findable.

- **Categories** (left tree): right‑click for Add Sub‑Category, Add Root‑Category, Edit, Export, Delete; **drag** to reorder or move. Each mod lives in one category (which drives the one‑active rule).
- **Tags:** add free‑form labels in a mod's **Edit** screen; filter the list by tag. Remote‑imported mods are auto‑tagged.
- **Search:** the box matches title, tags and id, with operators — space = AND, `OR`, `NOT`/`-`, `field:value`, `"exact phrase"`.
- **Edit a mod** (right‑click → Edit): name, author, category, tags, grading, description.
- **Previews:** set the thumbnail and manage a mod's images.

---

## 5. Customize a Mod's Keys & Config

**Goal:** change a mod's toggle keys or tweak its settings.

1. **Load the mod first** — keybindings and config come from its extracted files (if the option is greyed out, this is why).
2. Right‑click → **Keybindings**. For each binding you can:
   - **Rebind:** focus the field and **press** the key/combo to capture it (modifiers and `NO_…` exclusions supported; add controller buttons with the **Xbox button picker**).
   - Change the **type** (cycle / hold / toggle) and edit the **cycle values** / **condition**.
   - Add **multiple keys** to one binding, and **drag to reorder** bindings.
3. For deeper tweaks, right‑click → **`.ini` editor**: edit safe `key = value` lines (`[Key*]`, `[Constants]` tunables). Hash/override sections are **locked** — editing a hash breaks the mod.

---

## 6. Fix Mods After a Game Update

**Goal:** make mods render again after the game changes hashes.

1. **Close the game** (in‑use files can't be modified).
2. Add your fix tools once under **Settings → Fix Tools**.
3. Run **Tools → Analysis** to see which mods are broken (bad/missing hashes, conflicts, missing keybinds) — each mod gets a health badge.
4. Right‑click a mod → **Fix** (or fix a whole **category** at once). The fix patches the mod and re‑saves its archive automatically.
5. Re‑run Analysis or launch to confirm.

---

## 7. Combine & Slim Down Mods

**Goal:** advanced editing of your library.

- **Merge variants:** select several same‑character mods → **Merge** → pick a **swap key** that cycles between them in‑game. A new merged mod is created (originals kept); it's namespace‑based so each variant keeps its own keys. *Verify the swap key in‑game after merging.*
- **Optimize:** right‑click → **Optimize** to de‑duplicate identical files and rewrite internal `filename` references — tidies bloated mods.

---

## 8. Download Mods from the Remote Library

**Goal:** browse mod sites in‑app and import with one flow. Built‑in sources: **Hui站 (huihui)** and **GameBanana** (plus custom sites).

1. **Add a library.** Open **library management** (库管理) → **Libraries** → add one = a **site + game** (e.g. *GameBanana · Zenless Zone Zero*). Adding it starts a background **sync** that builds a local index (instant search / sort / offline). Switch the active library from the toolbar dropdown.
2. **Browse & filter.** Search matches title + tags; sort by site order or newest; the **Downloaded** filter shows only what you've imported. Cards show a tag badge and a green **✓** when already imported.
3. **Open a mod** → detail page: gallery + tags (left), **download links** (right).
4. **Download & import.** Click a link:
   - **Cloudreve / direct** → downloads + imports **in‑app**.
   - **Quark** → downloads in‑app once you've logged in (section 9).
   - Other hosts → open in your browser.
   Confirm the dialog (file, size, host); optionally set a **category** (overrides tag rules) and an **unzip password**. The download + import runs in the background — watch **Activity**.
5. **After import**, the mod is in your library (tagged + categorized) and the entry shows **✓ Imported**. Open its detail → **View mod** to jump to it locally.

### Import tag rules (per library)
Each library has **ordered rules** mapping remote tags → a local category on import (first match wins; no match = uncategorized). Edit them in library management → the library's **Import rules** tab.

---

## 9. Accounts & Settings

**Goal:** log in to download hosts and tune the app.

- **Quark login** (**Settings → Online Storage → Log in**): an in‑app window opens **Quark's own login page** — sign in there (you never type a password into this app) and the session is captured automatically. The app then downloads your Quark files like the official desktop client does.
- **Settings tabs:**
  - **Mod Work** — work‑directory mode + game‑launch command (path + args, e.g. `--nogui --xxmi ZZMI`; some games need extras, e.g. WuWa `-DisableModule=streamline -dx11 -d3d11`).
  - **Import** — import behavior / defaults.
  - **Fix Tools** — the fix‑tool library.
  - **Global** — theme (light/dark), language (English/中文), log level, automatic updates.
  - **Online Storage** — download‑host accounts.

---

## 10. Background Tasks & Activity

**Goal:** track long operations.

Loading, importing, syncing, merging, fixing and analysis all run in the **background** so the app stays responsive. The **status bar** shows a running summary; click the **Activity** area for the full panel — per‑task progress, history and cancel.

---

## 11. Interface Map (quick reference)

- **Top navigation:** **Mods** (模组) · **Remote Library** (远程库) · **Tools** (工具) · **Settings** (设置).
- **Header:** profile selector · this Help.
- **Status bar (bottom):** **Launch** (启动) · **Presets** (预设) · loaded‑mods count · **Activity**.
- **Mod card states:** loaded · available · unavailable · orphaned.
- **Theme & language:** Settings → Global (both light and dark are supported).

---

## 12. Troubleshooting

- **Keybindings look empty?** Load the mod first — they come from the extracted files.
- **"Mod folder in use" / a fix or load fails?** Close the game first (it holds files open).
- **Mods stopped showing after a game update?** Run **Tools → Analysis**, then **Fix** the flagged mods.
- **Quark download fails?** Make sure you're logged in (Settings → Online Storage). Large files download via the desktop‑client path; if a file is genuinely restricted, download it from Quark directly.
- **Experimenting is safe:** nothing here injects into a running game, so loading / fixing / deploying while the game is closed can't disrupt anything live.

---

## About

D3dxSkinManager is open source — report issues and get updates on GitHub:
<https://github.com/JiarongGu/D3dxSkinManager>

It **complements** XXMI (which installs importers, injects the mod DLL, and launches the game); this app is your compressed mod library, organizer, fixer and deployer.

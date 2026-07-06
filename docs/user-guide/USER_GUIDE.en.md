# D3dxSkinManager — User Guide

D3dxSkinManager is a **mod library manager** for 3DMigoto‑based games (via XXMI): Genshin Impact (GIMI), Zenless Zone Zero (ZZMI), Honkai: Star Rail (SRMI), Wuthering Waves (WWMI), Honkai Impact (HIMI), Endfield (EFMI), and more.

It stores your mods **compressed**, lets you **organize, fix, edit, preview and deploy** them, and hands the actual in‑game injection to **XXMI**. Think of it as: *this app = your mod library + workshop; XXMI = the runtime that loads mods into the game.*

> **Core idea:** mods live compressed in your library. **Loading** a mod extracts and deploys it into the game's Mods folder; **unloading** removes it. Only **one mod per category** is active at a time — loading a mod automatically unloads the previous one in the same category.

---

## Getting Started

Three steps to a working setup:

1. **Create or select a Profile.** Click the **profile selector** in the top‑right header. Each profile has its own mod database, categories and settings — ideal for managing different games separately.
2. **Set your Work Directory** (**Settings → Mod Work**). This is where mods get deployed so the game can load them. Pick one of:
   - **App default** — the app manages its own internal folder.
   - **XXMI Launcher** *(recommended)* — point at your XXMI install folder, then choose the importer (e.g. `ZZMI`). This sets both the deploy target (the importer's `Mods` folder) **and** the game‑launch command in one step.
   - **Custom folder** — point at any 3DMigoto `Mods` folder.
3. **Import some mods** and **load** the ones you want (see *Managing Mods*), then **Launch** the game.

> If you use XXMI, choosing the importer stages a confirmation showing the work dir, deploy target, launcher path and launch args before applying — nothing changes silently.

---

## The Interface

- **Top navigation:** **Mods** (模组), **Remote Library** (远程库), **Tools** (工具), **Settings** (设置).
- **Header:** profile selector, presets, and this Help.
- **Status bar (bottom):** the **Launch** button (启动), **Presets** (预设), the loaded‑mods count, and the **Activity** area (background‑task progress + history).
- **Theme & language:** switch light/dark theme and English/中文 in **Settings → Global**.

---

## Profiles

A profile is a self‑contained workspace: its own mod library, categories, tags, presets and settings.

- **Create / switch:** use the header profile selector.
- **When to use multiple:** one per game, or per configuration you want to keep separate.
- Deleting a profile removes its database and settings (mod archives on disk follow the profile's directory).

---

## Managing Mods

The **Mods** tab is your library: the **category tree** on the left, the **mod grid** on the right.

### Loading & unloading
1. Click a mod card to **load** it — the app extracts it and deploys it into your work directory.
2. Loading a mod **unloads** any previously‑loaded mod in the **same category** (one active per category).
3. Click the loaded mod again (or use its unload action) to **unload** it.

A mod card shows its preview image, name, and state (loaded / available / unavailable / orphaned).

### Finding mods
- The **search box** matches title, tags and id. It supports operators: space = AND, `OR`, `NOT`/`-`, `field:value`, and `"exact phrase"`.
- Click a category to filter to it; switch to the "all" view to search across everything.

### Per‑mod actions (right‑click a mod)
- **Edit** metadata (name, author, category, tags, grading, description).
- **Replace content** from a file.
- **Set preview / manage images.**
- **Fix** (re‑fix hashes — see *Fixing Mods*).
- **Keybindings** and **`.ini` editor** (see *Keybindings & Config*).
- **Package** (export/share), **Merge**, **Optimize**, **Delete**.

> Many actions need the mod's extracted files. If something is greyed out, **load the mod first** — then its keybindings and config become editable.

---

## Categories

Categories organize your library as a tree and enforce the "one active per category" rule.

- **Right‑click the tree** for: Add Sub‑Category, Add Root‑Category, Edit, Export, Delete.
- **Drag** categories to reorder or move them.
- A mod belongs to one category; loading it unloads the category's currently‑active mod.

---

## Tags

Tags are free‑form labels for cross‑cutting organization (character, type, author…).

- Add tags in a mod's **Edit** screen.
- **Filter** the mod list by tag.
- Mods imported from the **Remote Library** are auto‑tagged with the remote entry's tags.

---

## Importing Mods

- **Drag & drop** an archive (`.zip`/`.7z`/`.rar`) or a folder onto the window.
- Or use the **Import** flow for a guided queue.
- The **import workflow** handles multi‑mod archives, naming and category assignment before the mods enter your library.
- **Packages:** export a mod (or a set) as a shareable package, and import packages others share with you.

> Downloads always get **normalized** into the app's storage format (extracted and re‑compressed), so odd containers or passwords never break loading later.

---

## Keybindings & Config

3DMigoto mods use **key toggles** (swap variants, toggle parts). This app lets you view and rebind them safely.

### Keybindings editor
1. **Load the mod first** — keybindings are read from the extracted files.
2. Open the mod's **keybindings**. For each binding you can:
   - **Rebind the key:** focus the field and **press** the key/combo to capture it (modifiers and `NO_…` exclusions supported; add controller buttons with the **Xbox button picker**).
   - Change the **type** (cycle / hold / toggle), edit the **cycle values** or **condition**.
   - Add **multiple keys** to one binding (keyboard + controller share state).
   - **Reorder** bindings by dragging (the order is saved as mod metadata).

### `.ini` config editor
- Edit safe `key = value` lines: `[Key*]` bindings and `[Constants]` tunables.
- **Advanced/hash sections are locked** (texture overrides, resources, shaders). Editing a hash breaks the mod, so those are read‑only by design.

---

## Fixing Mods

When a game updates, mod **hashes** can break and mods stop rendering — they need re‑fixing.

1. Add your fix tools under **Settings → Fix Tools** (per‑profile fix‑tool library).
2. **Fix a mod:** right‑click → **Fix**. Or **fix a whole category** at once.
3. The fix runs on the mod's working copy and re‑patches its archive automatically.

> Close the game before fixing (an in‑use folder can't be modified). After a game update, running **Tools → Analysis** first tells you which mods need attention.

---

## Merging Mods

Combine several same‑character variants into **one mod** you cycle with a swap key.

1. Select the mods to merge → **Merge**.
2. Choose a **swap key** (cycles between variants in‑game).
3. A new merged mod is created; the originals stay in your library untouched.

The merge is **namespace‑based**, so each variant keeps its own keybinds/toggles as separate sets.

> In‑game behavior (the swap key cycling variants) should be verified live after merging.

---

## Optimizing Mods

Right‑click → **Optimize** to shrink and tidy a mod: de‑duplicate identical files and rewrite internal `filename` references. Useful for bloated or messy mods.

---

## Remote Library

Browse remote mod sites **inside the app**, then download and import with one flow. Sources shipped today: **Hui站 (huihui)** and **GameBanana**; you can add custom sites.

### Set up a library
1. Open **library management** (库管理) from the Remote tab.
2. In **Libraries**, add a library = a **site + game** (e.g. *GameBanana · Zenless Zone Zero*). Adding it starts a background **sync** that builds a local index for instant search, sorting and offline browsing.
3. Switch the active library from the toolbar dropdown any time.

### Browse & filter
- The grid shows the synced index. **Search** matches title + tags; **sort** by site order or newest.
- **Downloaded** filter (toolbar) shows only entries you've already imported.
- Cards show a tag badge and a green **✓** when already imported.

### Download & import
1. Click a mod → its **detail** page: gallery + tags on the left, **download links** on the right.
2. Click a download:
   - **Cloudreve / direct** hosts download and import **in‑app**.
   - **Quark** downloads in‑app once you've logged in (see below).
   - Other hosts open in your browser.
3. Confirm the dialog (file, size, host) — optionally pick a **category** (overrides the library's tag rules) and an **unzip password** — then the download + import runs in the background (watch the **Activity** panel).
4. When it finishes, the mod is in your library (tagged and categorized) and the remote entry shows the **✓ Imported** badge. Open its detail → **View mod** to jump to it in your library.

### Import tag rules (per library)
Each library has **ordered rules** mapping remote tags → a local category on import (first match wins; no match = uncategorized). Edit them in library management → the library's **Import rules** tab.

### Quark accounts
Quark downloads need a login. In **Settings → Online Storage**, click **Log in** — an in‑app window opens **Quark's own login page**; sign in there (you never type a password into this app), and the session is captured automatically. The app then downloads your Quark files like the official client does.

---

## Presets

A preset is a saved **set of loaded mods**. Save your current loadout as a preset, then apply it later to restore exactly that set — handy for switching between curated combinations. Presets live in the header / status‑bar **Presets**.

---

## Tools

- **Mod Analysis** — scans your mods for health issues (bad/missing hashes, conflicts, dead overrides, missing keybinds) and shows a health badge per mod. Run it after a game update.
- **File Cleanup** — finds and removes orphaned files (temp files, stale caches, the remote‑image cache) to reclaim space.
- **Mod‑ID Migration** — migrates older hash‑based mod ids to stable GUIDs (one‑time housekeeping).
- **Fix Tools** — manage the per‑profile fix‑tool library used by *Fixing Mods*.

---

## Launching the Game

- Press **Launch** (启动) in the status bar to run your configured launch command.
- With **XXMI** mode, launch = XXMI injects the importer's DLL and starts the game with mod support.
- Configure the command in **Settings → Mod Work → Game launch**: the executable path + arguments (e.g. `--nogui --xxmi ZZMI`). Some games need extra args (e.g. WuWa: `-DisableModule=streamline -dx11 -d3d11`).

---

## Settings

- **Mod Work** — work‑directory mode (App default / XXMI Launcher / Custom folder) and the game‑launch command.
- **Import** — import behavior and defaults.
- **Fix Tools** — manage the fix‑tool library.
- **Global** — theme (light/dark), language (English/中文), log level, and automatic updates.
- **Online Storage** — log in/out of download‑host accounts (Quark).

---

## Activity & Background Tasks

Long operations — loading, importing, syncing, merging, fixing, analysis — run in the **background** so the app stays responsive. The **status bar** shows a running summary; click the **Activity** area to open the full panel with per‑task progress, history and cancel.

---

## Tips & Troubleshooting

- **Keybindings look empty?** Load the mod first — they come from the extracted files.
- **"Mod folder in use" / a fix or load fails?** Close the game first (it holds files open).
- **Mods stopped showing after a game update?** Run **Tools → Analysis**, then **Fix** the flagged mods.
- **Quark download fails?** Make sure you're logged in (Settings → Online Storage). Very large files download using the desktop‑client path; if a file is genuinely restricted, download it from Quark directly.
- **Both light and dark themes** are supported — switch in Settings → Global.
- Nothing you do here injects into a running game, so loading/fixing/deploying mods while the game is closed is safe to experiment with.

---

## About

D3dxSkinManager is open source. Report issues and find updates on GitHub:
<https://github.com/JiarongGu/D3dxSkinManager>

It complements — and does not replace — **XXMI** (which installs the importers, injects the mod DLL, and launches the game). This app is your compressed mod library, organizer, fixer and deployer.

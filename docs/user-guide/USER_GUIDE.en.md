# Overview

## Welcome

**D3dxSkinManager** is a companion app for **3DMigoto / XXMI** modding (Genshin, Zenless Zone Zero, Star Rail, Wuthering Waves, Endfield, and other XXMI‑supported games).

It keeps your mods in a compressed **library** and helps you **organize, preview, fix, edit and deploy** them into XXMI's `Mods` folder — one click to turn a mod on, one to turn it off, one to launch. **XXMI still does the in‑game injection and launch;** this app is the library and workshop around it, so you spend less time shuffling files and more time modding.

> [!TIP]
> **The one rule to remember:** only **one mod per category** is active at a time. Turning on a mod automatically turns off the previous one in that same category.

**How to read these docs:** **Examples** are quick step‑by‑step walkthroughs — start there. **Features** and **Configuration** are the reference for details.

---

# Examples

## Get Started (first time)

> [!GOAL]
> Go from a fresh install to your first skin running in the game.

1. Click the **profile picker** in the top‑right and create a **profile** (a separate space per game).
2. Open **Settings → Mod Work** and choose **XXMI Launcher**. Point it at your XXMI folder and pick your game's importer (for example `ZZMI` for Zenless Zone Zero). This tells the app where to put mods **and** how to launch the game.
3. **Add a mod:** drag a mod's `.zip` / `.7z` file (or its folder) onto the window.
4. Open the **Mods** tab and **click the mod** to turn it on.
5. Click **Launch** (启动) at the bottom to start the game with your mod active.

> [!TIP]
> That's the whole loop: **add → turn on → launch.** Everything else is about doing it faster and keeping things tidy.

## Get a Mod from a Website

> [!GOAL]
> Download a skin from a mod site and use it — without leaving the app.

1. Open the **Remote Library** tab → **Manage libraries (库管理)** → add a library (choose a **site** and your **game**). The app fetches the list in the background.
2. **Browse or search**, then click a mod you like.
3. Click a **download** button on the mod's page.
4. Confirm the pop‑up (you can pick a category). The download and setup happen in the background — watch the **Activity** area at the bottom.
5. When it's done, the mod shows a green **✓**. Turn it on like any other mod and **Launch**.

> [!NOTE]
> Some sites (like Quark) ask you to sign in first — see **Configuration → Signing in to download sites**.

## My Mods Broke After a Game Update

> [!GOAL]
> Make your skins show up again after the game updates.

1. Open the **Tools** tab → **Analysis** to see which mods need attention.
2. Right‑click a flagged mod → **Fix** (or fix a whole category at once).
3. Reload in‑game — 3DMigoto/XXMI **hot‑reloads with F10** — or relaunch to see them.

> [!NOTE]
> You usually **don't** need to close the game — 3DMigoto hot‑reloads mods (F10). The app only can't overwrite a file the running game currently has **open**; if that happens it retries briefly, then reports it as *in use*. Then just retry, or close the game (or unload that mod in‑game) so the file is free, and fix again.

## Combine Two Outfits into One

> [!GOAL]
> Merge several versions of the same character so one key switches between them.

1. In the **Mods** list, select the versions you want to combine.
2. Click **Merge** and choose a **switch key**.
3. A new combined mod appears (your originals are kept). Turn it on and press the switch key in‑game to cycle looks.

---

# Features

## Profiles

A **profile** is a separate space with its own mods, categories and settings. Make one per game. Switch or create profiles from the picker in the top‑right corner.

## Mods

The **Mods** tab is your library — categories on the left, mods on the right.

- **Turn on / off:** click a mod to turn it on (the app copies it into the game folder). Turning one on turns off any other mod **in the same category**. Click again to turn it off.
- **Mod status:** each mod shows whether it's on, available, missing files, or orphaned.
- **Edit a mod:** right‑click → **Edit** to change its name, author, category, tags, rating and notes.
- **Pictures:** set a cover image and manage a mod's preview pictures.
- **More (right‑click):** replace files, fix, edit keys, package to share, merge, optimize, delete. *(Some options need the mod turned on first.)*

## Categories

Categories keep your library organized as a folder tree, and they're what enforce the "one mod on at a time" rule. Right‑click the tree to add, rename, export or delete a category; **drag** to rearrange.

## Tags & Search

- **Tags** are free labels (add them when editing a mod) so you can filter your library any way you like.
- **Search** looks through names, tags and ids. You can combine words, use `OR`, exclude with `-`, or search an exact `"phrase"`.

## Keys & Settings of a Mod

Many skins use keys to toggle parts or switch looks. **Turn the mod on first**, then right‑click it:

- **Keys:** click a key field and **press the key** you want (single keys, combos, or controller buttons). You can also change how a key behaves (tap / hold / cycle) and reorder keys by dragging.
- **Advanced settings:** an editor for the mod's own options. Safe settings are editable; the technical parts are locked so you can't accidentally break the mod.

## Fixing Mods

When a game updates, mods can stop showing until they're "fixed". Add your fix tools once (see Configuration), then right‑click a mod → **Fix**, or fix a whole category. The app repairs the mod and saves it for you.

## Merging Mods

Combine several versions of one character into a single mod you switch with a key. Select them → **Merge** → choose the key. Your originals stay untouched.

> [!NOTE]
> After merging, start the game and test the switch key to make sure it cycles the looks you expect.

## Optimizing Mods

Right‑click → **Optimize** to clean up a mod — it removes duplicate files to save space.

## Remote Library

Browse mod sites right inside the app and add mods with one click. Built‑in sites include **Hui站** and **GameBanana**, and you can add more.

- **Libraries:** each library is a **site + game**. Adding one downloads its mod list so search and browsing are instant.
- **Find mods:** search by name or tag, sort by newest, or use the **Downloaded** filter to see only what you already have. A green **✓** marks mods you've imported.
- **Download:** open a mod and pick a download. It's added to your library automatically — tagged and sorted into a category.
- **Jump to it:** an imported mod's page has a **View mod** button that takes you straight to it in your library.

## Presets

A **preset** remembers which mods you had turned on. Save your current setup, then load it later to bring back that exact combination — great for switching between favorite looks. Find presets at the bottom bar.

## Tools

- **Analysis** — checks your mods for problems and flags the ones that need a fix.
- **File Cleanup** — frees up space by removing leftover files.
- **Mod‑ID Migration** — a one‑time tidy‑up of older mods.
- **Fix Tools** — where you add the tools used to fix mods.

## Activity

Long jobs — turning on mods, downloading, syncing, fixing — run in the background so the app stays responsive. The bottom bar shows what's running; click the **Activity** area to see progress, history, and to cancel.

---

# Configuration

## Where Mods Go (Work Directory)

**Settings → Mod Work.** This is where the app places mods so the game loads them:

- **App default** — the app manages its own folder.
- **XXMI Launcher** *(recommended)* — point at your XXMI install and pick your game's importer. This sets both where mods go **and** how to launch the game.
- **Custom folder** — any 3DMigoto `Mods` folder you choose.

## Launching the Game

The **Launch** button at the bottom starts the game with mods active. If you're using XXMI, the app fills in the launch command for you. You can review or change it in **Settings → Mod Work → Game launch**.

## Signing in to Download Sites

Some sites need you to sign in before downloading. In **Settings → Online Storage**, click **Log in** — a window opens the site's **own login page**. Sign in there (you never type your password into this app). After that, the app can download from that site for you.

## Settings Reference

- **Mod Work** — where mods go + the launch command.
- **Import** — how new mods are brought in.
- **Fix Tools** — your mod‑fixing tools.
- **Global** — theme (light/dark), language (English/中文), and updates.
- **Online Storage** — your sign‑ins for download sites.

---

# About

## About & Help

D3dxSkinManager is free and open source. Find updates and report problems on GitHub:
<https://github.com/JiarongGu/D3dxSkinManager>

It works alongside **XXMI**, which actually loads mods into the game and starts it. This app is your library and workshop for collecting, organizing and fixing mods.

**Quick fixes:**
- *No keys shown for a mod?* Turn the mod on first.
- *A fix or turn‑on failed with "in use"?* The running game has that file open — retry (the app also auto‑retries briefly), or close the game / unload that mod in‑game, then redo. Otherwise 3DMigoto hot‑reload (F10) means you can keep the game open.
- *Mods vanished after an update?* Tools → Analysis, then Fix.
- *A download failed?* Make sure you're signed in (Settings → Online Storage).
- Mods deploy to the `Mods` folder and are loaded by 3DMigoto (hot‑reload with F10 while the game runs); the only limit is that a file the running game currently has open can't be overwritten (it reports *in use*).

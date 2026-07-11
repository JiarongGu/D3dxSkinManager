# Sensitive info — keep dev-machine + private data out of tracked files

**This repo is PUBLIC. Nothing that identifies the author's machine, their other projects, or private data may land in a tracked file, a commit message, or git history — not even after the working tree looks clean. Private context lives in the git-ignored `local/`.**

## Why

A 2026-07-12 audit of this public repo (`github.com/JiarongGu/D3dxSkinManager`) found three leak classes, each requiring a full `git filter-repo` rewrite (every SHA changed) + force-push to remove — an hour of irreversible surgery for data that never needed committing:

- **NSFW artifact** — an explicit `remote.png` doc screenshot (a topless mod thumbnail) survived in the object DB, anchored by an **orphaned release tag `v3.4`** a prior purge had missed.
- **Windows username** — the developer's user folder (`C:\Users\<user>\.nuget\...`) baked into an old `.csproj` `<Reference Update=...>` HintPath (Visual Studio wrote the absolute path).
- **Dev-machine layout + sibling-project name** — the real dev-folder root (`<drive>:\<dev-root>\...`) and a sibling project's real name, in blobs **and commit-message bodies** across 12 commits.

Prevention is ~free; remediation rewrites all of history. (Same rule exists in the sibling `a sibling project` repo — this is a cross-project standard.)

## The rules (never in a tracked file or commit message)

- **No absolute local paths.** `<drive>:\<dev-root>\…`, `C:\Users\<user>\…`, mapped-drive roots. Use a repo-relative path, a neutral placeholder (`<repo>`, `%USERPROFILE%`), or move the real path to `local/`.
- **No private-project names.** Sibling repos / the author's other apps **by name**. Refer to them generically — "a sibling project", "a proven pattern". The real name→path map lives only in `local/`.
- **No personal / network specifics.** Real host/NAS names, LAN IPs (`192.168.x.x`), the author's name/email in file *content* (authorship in git metadata / LICENSE is fine).
- **No explicit / NSFW imagery.** This app displays anime game skins — before committing ANY doc/user-guide screenshot, inspect every mod thumbnail for nudity. The veil corpus + any explicit material stay ONLY in git-ignored `devtools/fixtures/` or `local/` (see [content-veil.md](content-veil.md)).
- **No absolute NuGet HintPaths.** Prefer `PackageReference`; never let VS write `<Reference Update="C:\Users\...\.nuget\...">`.
- **Working/private files stay inside the repo** — `devtools/` for scratch/probes, git-ignored `local/` for private/backup. Never create a sibling/backup folder elsewhere under `Development/`.

## How to apply

- **Before committing, grep the diff for leaks:**
  `git diff --cached | grep -inE 'D:\\|C:\\Users|192\.168\.|<sibling-project-names>'`. Hit → move the value to `local/` and reference it generically.
- New machine/private context → write it to a file under git-ignored `local/`, not a tracked file.
- **Edge cases where the rule does NOT apply:** fictional example paths (`C:\Games\MyGame`, `D:\Games\MyGame`, `E:/Games/ZZZ`) and generic UNC (`\\host\share`) in docs/tests are fine — they reveal nothing real. The maintainer's own Git author name/email is the public GitHub identity — intentional, leave it.

## Remediation (a committed leak is a HISTORY problem, not a working-tree one)

Editing the current file leaves the value in every past commit + message (all public on push). Full flow → [git-history-blob-purge.md](git-history-blob-purge.md). Hard-won gotchas from the 2026-07-12 scrub:

- **filter-repo literal replacement is CASE-SENSITIVE** — add both the upper- and lower-case drive-letter form as separate rules; a lowercase drive-letter path once survived an uppercase-only pass.
- **Don't put the real sensitive strings in the rule/doc itself.** A `--replace-text` pass rewrites tracked files too, so a doc that quotes the real path/name gets mangled (or re-leaks). Document with angle-bracket placeholders (`<user>`, `<dev-root>`), never the literal.
- **A `.md` rule is NOT the place for a real example.** `--strip-blobs-with-ids <shaFile>` removes a specific blob (e.g. the NSFW image) by SHA everywhere in one pass — cleaner than path-based removal when the same path holds a good version elsewhere.
- **Abbreviations slip through.** A name scrubbed in full can survive as a shorthand (e.g. `D3dxSkinManager` → stray `D3dx`/`D3D`). Sweep short forms too.
- **Scrub blobs AND messages** — pass the rules file to BOTH `--replace-text` and `--replace-message`; personal paths often live in commit-message bodies.
- **Delete the offending tag AFTER the final filter-repo pass, not before** — a pre-deleted `v3.4` resurrected through the rewrite. Then `git reflog expire --expire=now --all && git gc --prune=now`; verify `git cat-file -t <sha>` → missing AND `git log --all -S<token>` → 0.
- **`git push --all` / `--tags` leaks more than you mean to.** Push the specific branch/tags you intend; don't blanket-push local-only refs.
- This shell env collapses `\\`→`\` in bash heredocs/python — author filter-repo rules with the **Write tool**, use **literal** rules (single backslash), not regex.
- A force-push does NOT guarantee GitHub dropped the blob (cache/forks/`/commit/<sha>` URLs) — for truly sensitive content, contact GitHub Support.

## Related

- [git-history-blob-purge.md](git-history-blob-purge.md) — the purge/rewrite mechanics.
- [use-project-paths.md](use-project-paths.md) — runtime counterpart (use path services, never raw temp/AppData).
- [scripts-live-in-repo.md](../rules/scripts-live-in-repo.md) — scratch lives in repo `devtools/`/`local/`, never `%TEMP%`.
- [content-veil.md](content-veil.md) — NSFW corpus is local/untracked by design.
- The sibling `a sibling project` repo carries the original (core-tier) version of this same rule — cross-project standard.

# Purge/replace a file blob in git history (e.g. an NSFW/wrong image committed + pushed)

When a committed **and pushed** file blob must be removed from ALL history — not just replaced going
forward (an NSFW/leaked/oversized image, a secret) — you rewrite history so the old blob never appears
in any commit, then force-push. Grounded on the `docs/user-guide/images/{library,remote}.png` NSFW-image
purge (2026-07-07). `git-filter-repo` is installed (`/c/Python/Python313/Scripts/git-filter-repo`).

## Compress repo images FIRST (they get baked into history permanently)
Doc/screenshot PNGs → pngquant before committing (see `in-app-guide.md`). The vendored binary:
`devtools/research/node_modules/pngquant-bin/vendor/pngquant.exe`.
```
pngquant.exe --quality=60-88 --strip --force --output out.min.png in.png
```
~60-65% smaller on real screenshots. Native res is fine for docs; only downscale (>2000px wide) if huge.
Read PNG dims cheaply with python `struct.unpack('>II', fh.read(8))` after skipping 16 bytes.

## Replace-a-blob-everywhere flow (the reliable one — `--blob-callback`)
Use this when the blob content should be SWAPPED (old image → new image) rather than the file removed.
Because the file blob is referenced by every commit since it was added, a callback that swaps the blob
by its original SHA fixes ALL of history in one pass AND leaves the working tree matching HEAD.

1. **Finalize + stash the new bytes OUTSIDE the tree.** filter-repo does a `git reset --hard` at the end,
   so copy the new (compressed) file to a **git-ignored** dir that survives the reset —
   `devtools/screenshots/` is git-ignored (verify: `git check-ignore <path>`).
2. **Capture the OLD blob SHAs:** `git rev-parse HEAD:path/to/file` for each target.
3. **Safety backup:** record `git rev-parse HEAD` and `git bundle create <ignored>/backup.bundle --all`
   (recoverable if the rewrite goes wrong). NOTE: the bundle CONTAINS the old blob — delete it at the end.
4. **Clean the working tree** (filter-repo refuses a dirty tree): `git checkout -- <the files>`.
5. **Run filter-repo** with a `--blob-callback` that swaps by `blob.original_id` (a bytes hex SHA):
   ```
   git filter-repo --force --blob-callback '
   if blob.original_id == b"<OLD_SHA_1>":
       with open("D:/…/devtools/screenshots/_new1.png","rb") as f: blob.data = f.read()
   elif blob.original_id == b"<OLD_SHA_2>":
       with open("D:/…/devtools/screenshots/_new2.png","rb") as f: blob.data = f.read()
   '
   ```
   Pass the callback via `--blob-callback "$(cat devtools/screenshots/_cb.py)"` (multi-line is fine).
   filter-repo rewrites every commit (SHAs change), repacks, prunes, and **removes the `origin` remote**.
6. **Verify the old blob is GONE:** `git cat-file -e <OLD_SHA>` → error, and
   `git rev-list --objects --all | grep <OLD_SHA>` → nothing. New blob SHA/size at HEAD matches the file.
7. **Re-add origin** (filter-repo strips it): `git remote add origin <url>`.
8. **Clean up scratch** incl. the backup bundle + callback (they hold the old blob).

To fully REMOVE a path (not swap) use `git filter-repo --path <p> --invert-paths --force` instead.

## Force-push (the outward, irreversible step — CONFIRM with the user first)
The whole branch + tags were rewritten, so:
```
git push origin master --force
git push origin --force --tags
```
Tags predating the rewritten commit keep their SHA (no-op); later ones move. **Never force-push shared
history without explicit user go-ahead** — here the user said "I will force push" and did it themselves.

## GitHub caveat (say this to the user)
A force-push does NOT guarantee the old blob is unreachable on GitHub: it can persist in GitHub's cache,
in **forks**, and via direct `.../blob/<SHA>` / commit URLs. For truly sensitive content the user must
contact **GitHub Support** to purge cached views + unreachable objects server-side.

## Recovery
Until the user force-pushes, `origin` (GitHub) still holds the OLD history — `git fetch` restores it.
Locally, the pre-rewrite state is in the backup bundle (if kept) or `git reflog` before filter-repo's gc.

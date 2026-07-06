# In-app user guide — markdown docs rendered by MarkdownView in the Help window

The end-user guide is authored ONCE as markdown and shown in two places: the repo docs AND the in-app
**Help & Documentation** window. Single source of truth — edit the markdown, both update.

## Source of truth
- `docs/user-guide/USER_GUIDE.en.md` + `USER_GUIDE.cn.md` — the guide (also linked from the repo `README.md`).
- Audience: **3DMigoto/XXMI modders** (non-technical, but they mod). Don't explain "what a mod is";
  explain how THIS app organizes/fixes/deploys mods around XXMI. Keep it task/example first.
- Structure = a **doc system** via markdown heading levels:
  - `# ` (H1) = a NAV GROUP: **Overview · Examples · Features · Configuration · About**.
  - `## ` (H2) = a PAGE within that group.
  - `### `/`#### ` = subsections inside a page.
- Font rule for the guide CONTENT (help context only): h2 page titles render 16px, h1 18px (scoped in
  `HelpWindow.css` — the ONE sanctioned exception to the 12/14 chrome rule, see `ui-design-rules.md`).

## Rendering
- **`shared/components/common/MarkdownView.tsx`** — a zero-dependency L1 atom markdown→React renderer
  (no external lib, so it survives the app's self-contained bundling). Supports: ATX headings, ordered +
  unordered lists (one level of nesting by indent), fenced code, `---` hr, images `![alt](src)`, inline
  **bold** / `` `code` `` / `[link](url)`, and **typed callouts** — a blockquote whose first line is
  `[!GOAL]` / `[!TIP]` / `[!NOTE]` / `[!INFO]` / `[!IMPORTANT]` / `[!WARNING]` renders as a colored,
  left-bordered box with an emoji icon (styles in `MarkdownView.css`). To add a callout type: add its
  icon to `CALLOUT_ICON` + a `.markdown-view__callout--<type>` color rule. Keep it zero-dep.
- **`modules/help/components/HelpWindow.tsx`** — raw-imports both md files via Vite `?raw`
  (`import guide from '../../../../../docs/user-guide/USER_GUIDE.en.md?raw'`), picks by `i18n.language`
  (`cn`/`zh` → cn), splits into `#` groups → `##` pages, renders a grouped icon nav + the active page.
  `?raw` from outside `src` works because `vite.config.ts` has `server.fs.allow: ['..']` (repo root) and
  build-time inlining isn't fs-gated; `vite/client` types the `*?raw` import (no custom d.ts needed).
- Opened from the status-bar version label (`AppStatusBar` → `App.tsx` `handleHelpClick`, a slide-in
  titled "Help & Documentation").

## When you add/rename an app feature
Update `docs/user-guide/USER_GUIDE.{en,cn}.md` (both languages) — that IS the in-app help. Keep the
`#`-group / `##`-page structure so the nav stays grouped. Screenshots in `docs/user-guide/images/` are
referenced by the README (repo-relative); the in-app guide stays screenshot-free (the user is already in
the app). Compress any repo image with **pngquant** (`devtools/research/node_modules/pngquant-bin`,
q60-88) — never commit a multi-hundred-KB raw PNG.

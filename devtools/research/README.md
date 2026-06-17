# research/ — web research tool (puppeteer + stealth)

Self-contained tool for grounding dev decisions in real sources (UI/UX patterns, library docs, API
behaviour) **instead of guessing** — and for fetching JS-rendered / anti-bot pages that the built-in
WebFetch can't. Modeled on the daily-planner `tools/puppeteer` design. stdout = JSON, stderr = logs.

## First-time install (one-off)

```
cd devtools/research && npm install
```
⚠️ Downloads Puppeteer's bundled Chromium (~280 MB) on first install. Not committed (git-ignored).
Until installed, use the built-in WebSearch/WebFetch for simple pages; use this for JS-rendered/anti-bot.

## Commands

### `npm run search -- "<query>" [--max N]`
Web search via DuckDuckGo's HTML endpoint (no API key) → `{query, count, results:[{title,url,snippet}]}`.
```
npm run search -- "video player keyboard shortcuts UX patterns" --max 8
```

### `npm run scrape -- <url> [--selector css] [--wait-for css] [--json|--html] [--timeout ms]`
Render a page (networkidle) and extract text / a selector / structured JSON.
```
npm run scrape -- https://example.com/docs --selector "main" --text
npm run scrape -- https://spa.example.com --wait-for ".loaded" --json
```

## How the agent calls it (allow-listed, prompt-free)

Wrapped by `devtools/research.mjs` so it's ONE allow-listed `node` call (no `cd` prefix):
```
node devtools/research.mjs search "<query>" [--max N]
node devtools/research.mjs scrape <url> [--selector css] [--json]
```
The wrapper auto-runs `npm install` here on first use. stdout is JSON → parse it; stderr is logs.

## Add a command
1. New `src/<name>.ts`, top-level async, `import { launchBrowser, newPage, emit, log } from './browser.js'`, JSON to stdout.
2. Add `"<name>": "tsx src/<name>.ts"` to `package.json#scripts`.
3. Add a case in `devtools/research.mjs` + a row here.
4. If high-frequency, add a `.claude/skills/<name>` wrapper.

## Project layout
```
devtools/research/
├── package.json · tsconfig.json · README.md
└── src/ { browser.ts (shared), search.ts, scrape.ts }
```

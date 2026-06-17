/**
 * Web search (DuckDuckGo HTML endpoint — no API key) → JSON results [{title, url, snippet}].
 * For UI/UX + technical research so the agent grounds decisions in sources instead of guessing.
 *
 *   npm run search -- "keyboard shortcut settings UX best practice" [--max 8]
 */
import { launchBrowser, newPage, emit, log } from './browser.js';

async function main() {
  const args = process.argv.slice(2);
  const query = args.find((a) => !a.startsWith('--'));
  const maxIdx = args.indexOf('--max');
  const max = maxIdx >= 0 ? Number(args[maxIdx + 1]) || 8 : 8;
  if (!query) {
    log('usage: npm run search -- "<query>" [--max N]');
    process.exit(2);
  }

  log(`searching: ${query}`);
  const browser = await launchBrowser();
  try {
    const page = await newPage(browser);
    await page.goto(`https://html.duckduckgo.com/html/?q=${encodeURIComponent(query)}`, {
      waitUntil: 'domcontentloaded',
      timeout: 30000,
    });
    const results = await page.evaluate((limit: number) => {
      const out: { title: string; url: string; snippet: string }[] = [];
      for (const el of Array.from(document.querySelectorAll('.result'))) {
        const a = el.querySelector<HTMLAnchorElement>('.result__a');
        const sn = el.querySelector('.result__snippet');
        if (!a) continue;
        let url = a.getAttribute('href') || '';
        // DDG wraps the real URL in uddg= — unwrap it.
        const m = url.match(/[?&]uddg=([^&]+)/);
        if (m) url = decodeURIComponent(m[1]);
        out.push({ title: a.textContent?.trim() || '', url, snippet: sn?.textContent?.trim() || '' });
        if (out.length >= limit) break;
      }
      return out;
    }, max);
    emit({ query, count: results.length, results });
    log(`${results.length} results`);
  } catch (e) {
    log(`error: ${(e as Error).message}`);
    emit({ query, count: 0, results: [], error: (e as Error).message });
    process.exitCode = 1;
  } finally {
    await browser.close();
  }
}

void main();

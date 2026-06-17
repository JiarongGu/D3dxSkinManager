/**
 * Scrape a JS-rendered page (the kind WebFetch can't get) → JSON. Optional CSS selector + wait-for.
 *
 *   npm run scrape -- <url> [--selector <css>] [--wait-for <css>] [--json] [--timeout <ms>]
 *   --text (default) = innerText · --json = [{tag,text,href,src}] for each match · --html = outerHTML
 */
import { launchBrowser, newPage, emit, log } from './browser.js';

function flag(args: string[], name: string): string | undefined {
  const i = args.indexOf(name);
  return i >= 0 ? args[i + 1] : undefined;
}

async function main() {
  const args = process.argv.slice(2);
  const url = args.find((a) => !a.startsWith('--') && /^https?:\/\//.test(a));
  if (!url) { log('usage: npm run scrape -- <url> [--selector css] [--wait-for css] [--json|--html]'); process.exit(2); }
  const selector = flag(args, '--selector');
  const waitFor = flag(args, '--wait-for');
  const mode = args.includes('--json') ? 'json' : args.includes('--html') ? 'html' : 'text';
  const timeout = Number(flag(args, '--timeout')) || 30000;

  log(`scraping: ${url}`);
  const browser = await launchBrowser();
  try {
    const page = await newPage(browser);
    await page.goto(url, { waitUntil: 'networkidle2', timeout });
    if (waitFor) await page.waitForSelector(waitFor, { timeout });
    const data = await page.evaluate(
      (sel: string | undefined, m: string) => {
        const els = sel ? Array.from(document.querySelectorAll(sel)) : [document.body];
        return els.map((el) => {
          if (m === 'html') return (el as HTMLElement).outerHTML;
          if (m === 'json')
            return {
              tag: el.tagName.toLowerCase(),
              text: (el as HTMLElement).innerText?.trim() || '',
              href: el.getAttribute('href') || undefined,
              src: el.getAttribute('src') || undefined,
            };
          return (el as HTMLElement).innerText?.trim() || '';
        });
      },
      selector,
      mode,
    );
    emit({ url, selector: selector ?? null, mode, count: data.length, data: selector ? data : data[0] });
    log(`scraped ${data.length} node(s)`);
  } catch (e) {
    log(`error: ${(e as Error).message}`);
    emit({ url, error: (e as Error).message });
    process.exitCode = 1;
  } finally {
    await browser.close();
  }
}

void main();

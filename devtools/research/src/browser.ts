/**
 * Shared puppeteer helpers for every research command. puppeteer-extra + stealth to get past basic
 * bot detection. stdout = JSON (machine-readable, one per invocation); stderr = logs (human).
 */
import puppeteer from 'puppeteer-extra';
import StealthPlugin from 'puppeteer-extra-plugin-stealth';
import type { Browser, Page } from 'puppeteer';

puppeteer.use(StealthPlugin());

const UA =
  'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36';

export async function launchBrowser(opts: { headless?: boolean } = {}): Promise<Browser> {
  const browser = await puppeteer.launch({
    headless: opts.headless ?? true,
    args: ['--no-sandbox', '--disable-setuid-sandbox', '--disable-blink-features=AutomationControlled'],
  });
  return browser as unknown as Browser;
}

export async function newPage(browser: Browser): Promise<Page> {
  const page = await browser.newPage();
  await page.setUserAgent(UA);
  await page.setViewport({ width: 1440, height: 900 });
  await page.setExtraHTTPHeaders({ 'accept-language': 'en-US,en;q=0.9' });
  return page;
}

/** Emit a JSON result to stdout (machine-readable). */
export function emit(value: unknown): void {
  process.stdout.write(JSON.stringify(value, null, 2) + '\n');
}

/** Log to stderr (doesn't pollute the stdout JSON). */
export function log(msg: string): void {
  process.stderr.write(`[research] ${msg}\n`);
}

export function sleep(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

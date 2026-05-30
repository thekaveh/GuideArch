/**
 * snapshot-all-tabs.mjs
 *
 * Visual snapshot script for GuideArch TypeScript UI.
 * Runs headless Chromium, captures all tabs in empty and SAS-loaded states,
 * then tests that the weight edit updates the Results chart.
 *
 * Usage: node tests/visual/snapshot-all-tabs.mjs
 *
 * Prerequisites:
 *   1. pnpm dev must be startable (runs in background here)
 *   2. Playwright chromium must be installed:
 *      pnpm exec playwright install chromium
 */

import { chromium } from 'playwright';
import { spawn } from 'child_process';
import { fileURLToPath } from 'url';
import path from 'path';
import fs from 'fs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const SNAPSHOTS_DIR = path.join(__dirname, 'snapshots');
const TS_DIR = path.join(__dirname, '..', '..');

// Ensure snapshots dir exists
fs.mkdirSync(SNAPSHOTS_DIR, { recursive: true });

const APP_URL = 'http://localhost:1420/';
const VIEWPORT = { width: 1440, height: 900 };

const TABS = [
  'Decisions',
  'Alternatives',
  'Properties',
  'Coefficients',
  'Constraints',
  'Results',
  'Critical Decisions',
  'Critical Constraints',
];

// ---------------------------------------------------------------------------
// Dev server management
// ---------------------------------------------------------------------------

async function startDevServer() {
  console.log('[dev] Starting pnpm dev...');
  const proc = spawn('pnpm', ['dev'], {
    cwd: TS_DIR,
    stdio: ['ignore', 'pipe', 'pipe'],
    detached: false,
  });

  proc.stderr.on('data', (d) => {
    const msg = d.toString().trim();
    if (msg) console.log('[dev err]', msg);
  });

  // Wait for the server to report it's ready
  await new Promise((resolve, reject) => {
    const timeout = setTimeout(() => reject(new Error('Dev server timed out after 30s')), 30000);
    proc.stdout.on('data', (d) => {
      const text = d.toString();
      if (text.includes('localhost') || text.includes('ready') || text.includes('Local:')) {
        clearTimeout(timeout);
        resolve(undefined);
      }
    });
    proc.on('error', (err) => {
      clearTimeout(timeout);
      reject(err);
    });
    proc.on('exit', (code) => {
      if (code !== null) {
        clearTimeout(timeout);
        reject(new Error(`Dev server exited with code ${code}`));
      }
    });
  });

  console.log('[dev] Server started.');
  return proc;
}

async function waitForPort(port, timeoutMs = 15000) {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    try {
      const resp = await fetch(`http://localhost:${port}/`);
      if (resp.ok || resp.status === 200) return;
    } catch {
      // not ready yet
    }
    await new Promise((r) => setTimeout(r, 300));
  }
  throw new Error(`Port ${port} not ready after ${timeoutMs}ms`);
}

// ---------------------------------------------------------------------------
// Screenshot helpers
// ---------------------------------------------------------------------------

async function clickTab(page, tabName) {
  // Use exact text match to avoid "Decisions" matching "Critical Decisions"
  const btn = page.locator('nav.tab-strip button').filter({ hasText: new RegExp(`^${tabName}$`) });
  await btn.click();
  // Small settle for reactive updates
  await page.waitForTimeout(200);
}

async function screenshot(page, filename) {
  const filepath = path.join(SNAPSHOTS_DIR, filename);
  await page.screenshot({ path: filepath, fullPage: false });
  const stat = fs.statSync(filepath);
  const kb = (stat.size / 1024).toFixed(0);
  console.log(`  [snap] ${filename} — ${kb} KB`);
  return filepath;
}

// ---------------------------------------------------------------------------
// Main
// ---------------------------------------------------------------------------

async function main() {
  let devServer = null;

  try {
    // 1. Start dev server
    devServer = await startDevServer();
    await waitForPort(1420);
    console.log('[ready] App reachable at', APP_URL);

    // 2. Launch Chromium
    const browser = await chromium.launch({ headless: true });
    const context = await browser.newContext({
      viewport: VIEWPORT,
      colorScheme: 'dark',
    });
    const page = await context.newPage();

    // 3. Load the app
    await page.goto(APP_URL, { waitUntil: 'networkidle' });
    await page.waitForTimeout(500);

    // ── Empty state screenshots ──────────────────────────────────────────────
    console.log('\n[step 4] Capturing empty state of all tabs...');
    for (const tab of TABS) {
      await clickTab(page, tab);
      const slug = tab.toLowerCase().replace(/ /g, '-');
      await screenshot(page, `empty-${slug}.png`);
    }

    // ── Load SAS sample ──────────────────────────────────────────────────────
    console.log('\n[step 5] Opening Sample SAS...');
    // Click the "Open Sample SAS" button in the toolbar
    await page.click('button.btn-sample:has-text("Open Sample SAS")');
    // Wait for candidates to appear — the Results tab should show data
    await page.waitForTimeout(1000);

    // ── SAS-loaded screenshots ───────────────────────────────────────────────
    console.log('\n[step 6] Capturing SAS-loaded state of all tabs...');
    for (const tab of TABS) {
      await clickTab(page, tab);
      const slug = tab.toLowerCase().replace(/ /g, '-');
      await screenshot(page, `sas-${slug}.png`);
    }

    // ── Edit weight and re-screenshot results ────────────────────────────────
    console.log('\n[step 7] Editing a property weight and capturing Results...');
    // Go to Properties tab
    await clickTab(page, 'Properties');
    await page.waitForTimeout(300);

    // Find the first weight input and change its value
    const weightInput = page.locator('.weight-input').first();
    (await weightInput.triple_click)
      ? weightInput.triple_click()
      : weightInput.click({ clickCount: 3 });
    await weightInput.fill('5');
    await weightInput.press('Enter');
    await page.waitForTimeout(500);

    // Switch to Results tab and screenshot
    await clickTab(page, 'Results');
    await page.waitForTimeout(500);
    await screenshot(page, 'sas-after-edit-results.png');

    // ── Done ─────────────────────────────────────────────────────────────────
    await browser.close();

    console.log('\n[done] All snapshots saved to:', SNAPSHOTS_DIR);
    const files = fs.readdirSync(SNAPSHOTS_DIR).filter((f) => f.endsWith('.png'));
    console.log(`       ${files.length} files:`, files.join(', '));
  } finally {
    // 8. Kill dev server
    if (devServer) {
      console.log('\n[dev] Killing dev server...');
      devServer.kill('SIGTERM');
      // Give it a moment to clean up
      await new Promise((r) => setTimeout(r, 500));
    }
  }
}

main().catch((err) => {
  console.error('[error]', err);
  process.exit(1);
});

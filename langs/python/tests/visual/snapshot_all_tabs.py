"""Visual snapshot script — captures screenshots of every UI tab.

Usage:
    cd langs/python && uv run python tests/visual/snapshot_all_tabs.py

What it does:
  1. Starts `uv run guidearch --port 8795` in the background
  2. Launches Chromium at 1440x900 viewport, dark color scheme
  3. Loads http://localhost:8795/
  4. Screenshots empty state of every tab → snapshots/empty-<tab>.png
  5. Clicks "Open Sample SAS"
  6. Screenshots loaded state of every tab → snapshots/sas-<tab>.png
  7. Edits a property weight, screenshots Results tab again → snapshots/sas-after-edit-results.png
  8. Kills the server

Tabs covered: Decisions, Alternatives, Properties, Coefficients, Constraints,
Results, Critical Decisions, Critical Constraints
"""

from __future__ import annotations

import subprocess
import sys
import time
from pathlib import Path

from playwright.sync_api import Page, ViewportSize, sync_playwright

# ---------------------------------------------------------------------------
# Config
# ---------------------------------------------------------------------------

_PORT = 8795
_BASE_URL = f"http://localhost:{_PORT}"
_SNAPSHOTS_DIR = Path(__file__).parent / "snapshots"
_VIEWPORT: ViewportSize = {"width": 1440, "height": 900}

# Tab names as they appear in the UI
_TAB_NAMES = [
    "Decisions",
    "Alternatives",
    "Properties",
    "Coefficients",
    "Constraints",
    "Results",
    "Critical Decisions",
    "Critical Constraints",
]

# Slug used for filenames
_TAB_SLUGS = [
    "decisions",
    "alternatives",
    "properties",
    "coefficients",
    "constraints",
    "results",
    "critical-decisions",
    "critical-constraints",
]


def _slug(name: str) -> str:
    return name.lower().replace(" ", "-")


def _wait_for_server(url: str, timeout: float = 30.0) -> bool:
    """Poll until the server responds or timeout."""
    import urllib.request

    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        try:
            urllib.request.urlopen(url, timeout=2)
            return True
        except Exception:
            time.sleep(0.5)
    return False


def _screenshot_all_tabs(page: Page, prefix: str) -> list[str]:
    """Click each tab and take a screenshot. Returns list of saved paths."""
    saved: list[str] = []
    for name, slug in zip(_TAB_NAMES, _TAB_SLUGS, strict=False):
        # Click the tab — use exact=True to avoid strict mode violations for
        # tabs like "Decisions" that also match "Critical Decisions"
        try:
            page.get_by_role("tab", name=name, exact=True).click()
            page.wait_for_timeout(900)  # let content render
        except Exception as exc:
            print(f"  Warning: could not click tab '{name}': {exc}")
            # Fallback: locate by tab text content
            try:
                page.locator(f".q-tab:has(.q-tab__label:text-is('{name}'))").click()
                page.wait_for_timeout(900)
            except Exception as exc2:
                print(f"  Fallback also failed: {exc2}")
                continue

        out_path = _SNAPSHOTS_DIR / f"{prefix}-{slug}.png"
        page.screenshot(path=str(out_path), full_page=False)
        size = out_path.stat().st_size if out_path.exists() else 0
        print(f"  Saved: {out_path.name}  ({size // 1024} KB)")
        saved.append(str(out_path))
    return saved


def main() -> None:
    _SNAPSHOTS_DIR.mkdir(parents=True, exist_ok=True)

    # ── 1. Start server ───────────────────────────────────────────────────────
    print(f"Starting guidearch on port {_PORT}...")
    # We need to run from the langs/python directory using uv
    _python_dir = Path(__file__).parents[2]
    import shutil

    uv_bin = shutil.which("uv") or "uv"
    server_proc = subprocess.Popen(
        [uv_bin, "run", "guidearch", "--port", str(_PORT)],
        cwd=str(_python_dir),
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )

    print(f"Waiting for server at {_BASE_URL}...")
    alive = _wait_for_server(_BASE_URL, timeout=30)
    if not alive:
        server_proc.kill()
        print("ERROR: Server did not start within 30 seconds.")
        sys.exit(1)

    print("Server is up.")
    time.sleep(2)  # extra settle time for NiceGUI

    try:
        with sync_playwright() as pw:
            browser = pw.chromium.launch(
                headless=True,
                args=["--no-sandbox", "--disable-dev-shm-usage"],
            )
            ctx = browser.new_context(
                viewport=_VIEWPORT,
                color_scheme="dark",
                device_scale_factor=1,
            )
            page = ctx.new_page()

            # ── 2. Load the app ───────────────────────────────────────────────
            # Use a dedicated page for empty-state screenshots to avoid VM state
            # pollution from any previous run.
            print(f"Loading {_BASE_URL}...")
            page.goto(_BASE_URL, wait_until="networkidle", timeout=30000)
            page.wait_for_timeout(3000)  # extra settle for NiceGUI JS hydration

            # ── 3. Screenshot empty state of every tab ────────────────────────
            # Capture empty state FIRST before loading any scenario.
            # The VM starts with no scenario on a fresh server start.
            # If decisions appear (from a prior session), click "New" to clear.
            status_bar = page.locator(".guidearch-statusbar")
            if status_bar.count() > 0:
                status_text = status_bar.inner_text()
                if "No scenario" not in status_text and status_text.strip():
                    state_preview = status_text[:40]
                    print(
                        f"  Server has existing scenario state ({state_preview}), clicking New..."
                    )
                    try:
                        page.get_by_role("button", name="New").click()
                        page.wait_for_timeout(1500)
                    except Exception as exc:
                        print(f"  Warning: could not click New: {exc}")

            print("\n=== Empty state screenshots ===")
            _screenshot_all_tabs(page, "empty")

            # ── 4. Click "Open Sample SAS" ────────────────────────────────────
            print("\nClicking 'Open Sample SAS'...")
            try:
                # The button text from main.py is "Open Sample SAS"
                btn = page.get_by_role("button", name="Open Sample SAS")
                btn.click()
                page.wait_for_timeout(3000)  # wait for solve
                print("  SAS loaded.")
            except Exception as exc:
                print(f"  Warning: could not click 'Open Sample SAS': {exc}")
                # Try alternative — look for any button with SAS in it
                try:
                    page.locator("button:has-text('SAS')").first.click()
                    page.wait_for_timeout(3000)
                    print("  SAS loaded (fallback).")
                except Exception as exc2:
                    print(f"  Fallback also failed: {exc2}")

            # ── 5. Screenshot loaded state of every tab ───────────────────────
            print("\n=== SAS loaded state screenshots ===")
            _screenshot_all_tabs(page, "sas")

            # ── 6. Edit a property weight and re-screenshot Results ───────────
            print("\nEditing property weight...")
            try:
                # Go to Properties tab
                page.get_by_role("tab", name="Properties", exact=True).click()
                page.wait_for_timeout(800)

                # Find the first weight input (a number field with label "Weight")
                weight_inputs = page.locator("input[type='number']")
                cnt = weight_inputs.count()
                if cnt > 0:
                    first_weight = weight_inputs.first
                    first_weight.click()
                    first_weight.fill("5")
                    # Trigger blur by pressing Tab
                    first_weight.press("Tab")
                    page.wait_for_timeout(2000)
                    print(f"  Weight edited ({cnt} number inputs found).")
                else:
                    print("  No number inputs found; skipping weight edit.")
            except Exception as exc:
                print(f"  Weight edit failed: {exc}")

            # Screenshot Results tab after edit
            try:
                page.get_by_role("tab", name="Results").click()
                page.wait_for_timeout(1500)
                out_path = _SNAPSHOTS_DIR / "sas-after-edit-results.png"
                page.screenshot(path=str(out_path), full_page=False)
                size = out_path.stat().st_size if out_path.exists() else 0
                print(f"\nSaved: {out_path.name}  ({size // 1024} KB)")
            except Exception as exc:
                print(f"  Could not screenshot Results after edit: {exc}")

            ctx.close()
            browser.close()

    finally:
        # ── 7. Kill the server ────────────────────────────────────────────────
        print("\nKilling server...")
        server_proc.terminate()
        try:
            server_proc.wait(timeout=5)
        except subprocess.TimeoutExpired:
            server_proc.kill()
        print("Done.")


if __name__ == "__main__":
    main()

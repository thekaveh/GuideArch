"""Design-system theme for GuideArch (NiceGUI / Quasar).

Exposes:
  TOKENS  - dict of all design-system §2 color tokens (hex strings).
  inject_css() - injects :root CSS variables + body font into the page head,
                 then configures Quasar's primary/secondary/positive/negative/
                 info/warning accent colors via ui.colors().

Usage (call once, early in index()):
    from guidearch.view.theme import inject_css
    inject_css()
"""

from __future__ import annotations

from nicegui import ui

# ---------------------------------------------------------------------------
# §2 Color tokens — every hex value taken literally from the design system.
# ---------------------------------------------------------------------------

TOKENS: dict[str, str] = {
    # §2.1 Surface
    "bg-page": "#0b0d12",
    "bg-surface": "#13161d",
    "bg-surface-2": "#1a1e27",
    "bg-surface-3": "#252a36",
    "border-subtle": "#262b36",
    "border-strong": "#363c4a",
    # §2.2 Text
    "text-primary": "#e6e7ed",
    "text-secondary": "#9298a8",
    "text-muted": "#5e6478",
    "text-inverse": "#0b0d12",
    # §2.3 Accent
    "accent": "#8b5cf6",
    "accent-hover": "#a78bfa",
    "accent-muted": "#3d2a6b",
    "accent-on": "#ffffff",
    # §2.4 Semantic
    "success": "#10b981",
    "warning": "#f59e0b",
    "danger": "#ef4444",
    "info": "#3b82f6",
    # §2.5 Fuzzy axes (charts only)
    "fuzzy-positive": "#34d399",
    "fuzzy-average": "#fbbf24",
    "fuzzy-negative": "#fb7185",
}

# ---------------------------------------------------------------------------
# §3 Type families
# ---------------------------------------------------------------------------

_FONT_SANS = (
    "-apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, "
    "\"Helvetica Neue\", Arial, sans-serif"
)
_FONT_MONO = (
    "\"Cascadia Code\", \"JetBrains Mono\", \"SF Mono\", Consolas, monospace"
)


def inject_css() -> None:
    """Inject design-system CSS variables and Quasar color config into the page.

    Must be called before any UI elements are rendered in the page function.
    """
    # Build :root CSS-variable block from TOKENS
    css_vars = "\n".join(
        f"  --{name}: {value};" for name, value in TOKENS.items()
    )

    css = f"""
:root {{
{css_vars}
}}

body {{
  background: var(--bg-page);
  color: var(--text-primary);
  font-family: {_FONT_SANS};
  font-size: 14px;
  font-weight: 400;
}}

/* §7 Motion: 120 ms ease-out hover transitions */
* {{
  transition-duration: 120ms;
  transition-timing-function: ease-out;
}}

/* §5.2 Inputs: 32px height, 6px radius, bg-surface-2, border-strong */
.q-field__control {{
  height: 32px !important;
  border-radius: 6px !important;
  background: var(--bg-surface-2) !important;
}}
.q-field--outlined .q-field__control:before {{
  border-color: var(--border-strong) !important;
  border-radius: 6px !important;
}}
.q-field--outlined.q-field--focused .q-field__control:before {{
  border-color: var(--accent) !important;
  border-width: 2px !important;
}}

/* §5.3 Tables: body rows 36px, header text-secondary */
.q-table th {{
  background: var(--bg-surface) !important;
  color: var(--text-secondary) !important;
  font-size: 12px !important;
  height: 32px !important;
}}
.q-table tbody tr {{
  height: 36px !important;
  border-bottom: 1px solid var(--border-subtle) !important;
}}
.q-table tbody tr:hover {{
  background: var(--bg-surface-2) !important;
}}

/* §5.4 Tabs: 40px strip, border-subtle underline */
.q-tabs {{
  height: 40px !important;
  border-bottom: 1px solid var(--border-subtle) !important;
}}
.q-tab {{
  color: var(--text-secondary) !important;
  font-size: 14px !important;
}}
.q-tab__label {{
  text-transform: none !important;
  font-size: 13px !important;
  font-weight: 500 !important;
  letter-spacing: 0 !important;
}}
.q-tab--active {{
  color: var(--text-primary) !important;
}}
.q-tab--active .q-tab__label {{
  color: var(--text-primary) !important;
  font-weight: 600 !important;
}}
.q-tab__indicator {{
  background: var(--accent) !important;
  height: 2px !important;
}}

/* Monospace for ID / numeric columns */
.font-mono {{
  font-family: {_FONT_MONO};
  font-variant-numeric: tabular-nums;
}}

/* §5.5 Cards */
.q-card {{
  background: var(--bg-surface) !important;
  border: 1px solid var(--border-strong) !important;
  border-radius: 8px !important;
}}

/* §5.1 Buttons: no text-transform */
.q-btn .q-btn__content span {{
  text-transform: none !important;
  letter-spacing: 0 !important;
  font-weight: 600 !important;
}}

/* Disable Quasar table zebra stripes */
.q-table tbody tr:nth-child(even) {{
  background: var(--bg-page) !important;
}}
.q-table tbody tr:nth-child(odd) {{
  background: var(--bg-page) !important;
}}

/* Ensure table body background is correct */
.q-table__middle {{
  background: var(--bg-page) !important;
}}

/* §5.6 Status chips — small, compact */
.q-badge {{
  font-size: 11px !important;
  font-weight: 500 !important;
  padding: 3px 7px !important;
  border-radius: 10px !important;
}}

/* Toolbar height §6 */
.guidearch-toolbar {{
  height: 56px !important;
  padding: 0 24px !important;
}}

/* Status bar §6 */
.guidearch-statusbar {{
  height: 32px !important;
  padding: 0 24px !important;
  font-size: 12px !important;
  color: var(--text-secondary) !important;
  font-family: {_FONT_SANS};
}}

/* NiceGUI content reset */
.nicegui-content {{
  padding: 0 !important;
}}
"""

    ui.add_head_html(f"<style>{css}</style>")

    # Configure Quasar accent palette to match §2.3 / §2.4 semantic tokens
    ui.colors(
        primary="#8b5cf6",    # accent
        secondary="#9298a8",  # text-secondary (used as Quasar secondary)
        positive="#10b981",   # success
        negative="#ef4444",   # danger
        info="#3b82f6",       # info
        warning="#f59e0b",    # warning
    )

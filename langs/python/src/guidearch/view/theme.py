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

# §2-light — elevated light theme. Same keys as TOKENS; values retuned for
# contrast on white. Applied via a body.body--light override block (Quasar
# adds that class when ui.dark_mode() is disabled).
LIGHT_TOKENS: dict[str, str] = {
    # §2.1 Surface
    "bg-page": "#ffffff",
    "bg-surface": "#fbfbfd",
    "bg-surface-2": "#f3f4f8",
    "bg-surface-3": "#eaecf3",
    "border-subtle": "#eef0f4",
    "border-strong": "#dfe2ec",
    # §2.2 Text
    "text-primary": "#1a1f36",
    "text-secondary": "#5a6072",
    "text-muted": "#8a90a2",
    "text-inverse": "#ffffff",
    # §2.3 Accent
    "accent": "#6b4ce0",
    "accent-hover": "#5a3dd0",
    "accent-muted": "#f0ecfd",
    "accent-on": "#ffffff",
    # §2.4 Semantic
    "success": "#0ea371",
    "warning": "#b8801a",
    "danger": "#dc3a3a",
    "info": "#3a6fd8",
    # §2.5 Fuzzy axes (charts only)
    "fuzzy-positive": "#0ea371",
    "fuzzy-average": "#d9a014",
    "fuzzy-negative": "#e5566f",
}

# ---------------------------------------------------------------------------
# §3 Type families
# ---------------------------------------------------------------------------

_FONT_SANS = (
    '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif'
)
_FONT_MONO = '"Cascadia Code", "JetBrains Mono", "SF Mono", Consolas, monospace'


def inject_css() -> None:
    """Inject design-system CSS variables and Quasar color config into the page.

    Must be called before any UI elements are rendered in the page function.
    """
    # Build :root CSS-variable block from TOKENS
    css_vars = "\n".join(f"  --{name}: {value};" for name, value in TOKENS.items())
    light_vars = "\n".join(f"  --{name}: {value};" for name, value in LIGHT_TOKENS.items())

    css = f"""
:root {{
{css_vars}
}}

/* §2-light — applied via BOTH Quasar's body--light class AND our own
   html[data-theme='light'] marker (set explicitly in _apply_theme). Relying on
   Quasar's class alone was unreliable (it depends on dark-mode timing / the
   ui.run(dark=...) global), which left the page on light surfaces but with the
   dark text tokens — "light on light". html[data-theme] beats :root so the
   light values win deterministically. */
body.body--light,
body.gx-light {{
{light_vars}
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

/* §5.2 Inputs: 32px (min) height, 6px radius, bg-surface-2, border-strong.
   min-height (not a fixed height) so an outlined field with a floating label
   can grow enough for the label to clear the value — a hard 32px crammed the
   label onto the content ("no margin between Name and its value"). */
.q-field__control {{
  min-height: 32px !important;
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
/* Input text / label / placeholder pull from tokens so fields are correct in
   BOTH themes without the hardcoded Quasar `dark` prop (which forced light text
   and broke the light theme). */
.q-field__native,
.q-field__input,
.q-field__native span {{
  color: var(--text-primary) !important;
}}
.q-field__label {{
  color: var(--text-secondary) !important;
}}
.q-field__native::placeholder {{
  color: var(--text-muted) !important;
}}
.q-field__marginal,
.q-field__append .q-icon,
.q-field__prepend .q-icon {{
  color: var(--text-secondary) !important;
}}

/* Toolbar buttons: tidy, consistent icon size + height so the File/Sample
   icons sit aligned (Quasar's default 24px Material icon dwarfed the 13px
   labels and read as sloppy). */
.guidearch-toolbar .q-btn {{
  min-height: 32px !important;
}}
.guidearch-toolbar .q-btn .q-icon {{
  font-size: 18px !important;
}}

/* §5.3 Tables: body rows 36px, header text-secondary */
.q-table th {{
  background: var(--bg-surface) !important;
  color: var(--text-secondary) !important;
  font-size: 12px !important;
  height: 32px !important;
  position: sticky !important;
  top: 0 !important;
  z-index: 1 !important;
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

/* §3.2 Solve — the single loudest control. Dark = accent gradient + glow. */
.guidearch-solve {{
  background: linear-gradient(135deg, var(--accent), var(--accent-hover)) !important;
  color: var(--accent-on) !important;
  box-shadow:
    0 0 0 1px var(--accent),
    0 2px 12px color-mix(in srgb, var(--accent) 45%, transparent) !important;
  transition: box-shadow 120ms ease-out, filter 120ms ease-out !important;
}}
.guidearch-solve:hover {{
  filter: brightness(1.05);
  box-shadow:
    0 0 0 1px var(--accent-hover),
    0 4px 18px color-mix(in srgb, var(--accent) 55%, transparent) !important;
}}
.guidearch-solve.disabled,
.guidearch-solve[disabled] {{
  box-shadow: none !important;
  filter: none !important;
}}

/* Light theme: flat accent fill + soft drop-shadow (no gradient, no glow). */
body.body--light .guidearch-solve,
body.gx-light .guidearch-solve {{
  background: var(--accent) !important;
  box-shadow:
    0 1px 3px color-mix(in srgb, var(--accent) 30%, transparent),
    0 1px 2px rgba(0, 0, 0, 0.06) !important;
}}
body.body--light .guidearch-solve:hover,
body.gx-light .guidearch-solve:hover {{
  background: var(--accent-hover) !important;
  box-shadow:
    0 2px 6px color-mix(in srgb, var(--accent) 35%, transparent),
    0 1px 3px rgba(0, 0, 0, 0.08) !important;
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

    # Configure Quasar accent palette to match §2.3 / §2.4 semantic tokens.
    # Pull from TOKENS so a future palette tweak only needs to touch the
    # dict above; the duplicated hex literals here would silently drift.
    ui.colors(
        primary=TOKENS["accent"],
        secondary=TOKENS["text-secondary"],
        positive=TOKENS["success"],
        negative=TOKENS["danger"],
        info=TOKENS["info"],
        warning=TOKENS["warning"],
    )


def active_chart_tokens(theme: str) -> dict[str, str]:
    """Return the token→hex map for the active theme.

    Chart option builders (chart_data.py) read concrete hex from this map so
    ECharts plots use the correct per-theme colors. The TS/C# charts retint via
    CSS vars / theme brushes; Python's ECharts options are JSON, so we resolve
    the active theme's hex here at option-build time.
    """
    return LIGHT_TOKENS if theme == "light" else TOKENS

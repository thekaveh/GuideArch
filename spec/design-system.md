# GuideArch Design System

**Status:** Authoritative for v1.x UI work. Applies to all three impls.

The M1–M4 UIs were functional but visually unpolished. This spec defines the design language all three impls implement, so the apps look like the same product.

## 1. Personality

- **Professional**, not playful. The audience is software architects evaluating significant technology decisions.
- **Dense but not cramped.** Information per screen is high (lots of decisions, alternatives, properties), so we use compact controls and a tight type scale.
- **Confident dark theme.** Light theme is deferred to v1.x.
- **Quiet color**. Color carries meaning (status, criticality, fuzzy axis), not decoration.

## 2. Color tokens

Use these literal hex values. Every impl maps them into its native theming system (Tailwind for NiceGUI/Svelte, FluentTheme for Avalonia).

### 2.1 Surface

| Token | Hex | Use |
|---|---|---|
| `bg-page` | `#0b0d12` | Page background |
| `bg-surface` | `#13161d` | Cards, panels, table backgrounds |
| `bg-surface-2` | `#1a1e27` | Elevated surfaces, modal backgrounds, hover-row highlight |
| `bg-surface-3` | `#252a36` | Heavily-elevated surfaces (popovers) |
| `border-subtle` | `#262b36` | Hairlines between rows, between panels |
| `border-strong` | `#363c4a` | Card borders, input borders |

### 2.2 Text

| Token | Hex | Use |
|---|---|---|
| `text-primary` | `#e6e7ed` | Body text, primary labels |
| `text-secondary` | `#9298a8` | Secondary labels, captions, metadata |
| `text-muted` | `#5e6478` | Disabled text, placeholders |
| `text-inverse` | `#0b0d12` | Text on colored backgrounds |

### 2.3 Accent — single brand color (indigo-violet)

| Token | Hex | Use |
|---|---|---|
| `accent` | `#8b5cf6` | Primary buttons, focus rings, active tab indicators |
| `accent-hover` | `#a78bfa` | Button hover |
| `accent-muted` | `#3d2a6b` | Selected-row tint, accent backgrounds |
| `accent-on` | `#ffffff` | Text on accent button |

### 2.4 Semantic (status)

| Token | Hex | Use |
|---|---|---|
| `success` | `#10b981` | Positive status (saved, conforming) |
| `warning` | `#f59e0b` | Soft warnings (lower>modal, redundant constraint, unsaved-changes chip) |
| `danger` | `#ef4444` | Fatal errors, destructive actions (Delete) |
| `info` | `#3b82f6` | Informational chips |

### 2.5 Fuzzy axes (only for charts)

| Token | Hex | Use |
|---|---|---|
| `fuzzy-positive` | `#34d399` | Positive (optimistic) vertex / dimension |
| `fuzzy-average` | `#fbbf24` | Modal / average vertex |
| `fuzzy-negative` | `#fb7185` | Negative (pessimistic) vertex |

## 3. Type

- **Family:** system sans (`-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, ...`); monospace for IDs and numbers (`"Cascadia Code", "JetBrains Mono", "SF Mono", Consolas, monospace`).
- **Scale:** 12 / 13 / 14 / 16 / 20 / 24 / 32 (px).
- **Weights:** 400 (body), 500 (emphasis), 600 (headings, button labels), 700 (large headings only).
- **Numeric:** use `font-variant-numeric: tabular-nums` for any numeric column so digits align.

Typical mapping:

| Role | Size | Weight |
|---|---|---|
| Page title (h1) | 24 | 600 |
| Section heading (h2) | 16 | 600 |
| Body text | 14 | 400 |
| Label | 13 | 500 |
| Caption / metadata | 12 | 400 |
| Numeric (table) | 13 | 400 tabular |
| Code / ID | 12 | 400 monospace |

## 4. Spacing

8-pixel base scale: 4 / 8 / 12 / 16 / 24 / 32 / 48 (px).

| Use | Spacing |
|---|---|
| Inline gap between sibling buttons / labels | 8 |
| Vertical gap between table rows | 4 (with 1px border-subtle) |
| Padding inside cards / panels | 16 |
| Padding inside primary buttons | 8 / 16 (v / h) |
| Padding inside inputs | 8 / 12 (v / h) |
| Tab strip vertical padding | 12 |
| Page margins | 24 |

## 5. Components

### 5.1 Buttons

- **Primary** — solid `accent` background, `accent-on` text, 8/16 padding, 6px radius, `font-weight: 600`, hover → `accent-hover`. Used for the principal action of a view (e.g. **Open Sample SAS**).
- **Secondary** — transparent background, `text-primary` text, `border-strong` 1px border, same shape. Hover → `bg-surface-2`.
- **Ghost** — transparent, `text-secondary` text, no border. Hover → `bg-surface-2`. Used for toolbar actions.
- **Destructive** — same shape as Secondary but `danger` text and border. Confirm-then-act for Delete.
- **Icon-only** — 32×32 square, ghost, 4px radius. Tooltip required.

### 5.2 Inputs

- 32px height (default density). 6px radius. `bg-surface-2` background. 1px `border-strong` border. Focus → 2px `accent` border + inset focus ring.
- `font-size: 14`, `padding: 0 12`.

### 5.3 Tables

- Header row: `bg-surface`, `text-secondary`, 12px label, 32px row height, sticky.
- Body rows: `bg-page`, 36px height, 1px `border-subtle` between rows.
- Hover: `bg-surface-2`.
- Selected: `accent-muted` background, 2px `accent` left border.
- Numeric columns: right-aligned, `font-variant-numeric: tabular-nums`, monospace for IDs.

### 5.4 Tabs

- Horizontal tab strip, 40px tall, `border-subtle` 1px under.
- Inactive tab: `text-secondary`, transparent background.
- Hover: `text-primary`, `bg-surface-2`.
- Active: `text-primary`, 2px `accent` indicator beneath.

### 5.5 Cards / panels

- `bg-surface`, 1px `border-strong`, 8px radius, 16px padding.

### 5.6 Status chips

- 20px tall, 4/8 padding, 10px radius.
- Variants: success / warning / danger / info — `bg-{semantic}` at 12% alpha, text in the corresponding semantic color.

### 5.7 Charts

- Background `bg-surface`.
- Axis labels: `text-secondary`, 12px.
- Grid lines: `border-subtle` at 50% alpha.
- Bars / lines: `accent`; fuzzy-decomposition triangles use the three fuzzy-axis tokens.

## 6. Layout

- **App shell**: `bg-page` everywhere. 1px `border-subtle` separates toolbar / tab strip / main / status bar.
- **Toolbar**: 56px tall, 24px horizontal padding, ghost buttons.
- **Status bar**: 32px tall, 24px horizontal padding, 12px text in `text-secondary`.
- **Main pane**: 24px padding around content. Vertical scroll inside tab body, not on the page.

## 7. Motion

- Minimal. Hover transitions on background-color over 120ms ease-out. No bouncing, no spinning, no entrance animations.
- Toast/notification appears with 200ms ease-out from top, auto-dismisses after 3s.

## 8. Empty states

When a tab renders with no data:

- Center the empty state vertically in the available area.
- 14px `text-secondary` headline; 13px `text-muted` body.
- Always provide a primary CTA: e.g. "Click **Open Sample SAS** in the toolbar to try the example."

## 9. Per-impl realization notes

- **TypeScript (Svelte)**: define the tokens as CSS custom properties on `:root` in a global `app.css` (or `+layout.svelte`). Inject before `+page.svelte` mounts. Components reference `var(--bg-page)` etc.
- **Python (NiceGUI / Quasar)**: NiceGUI exposes Tailwind 4 classes; map the tokens via `tailwind.config.js` (`ui.add_head_html` to inject a `<style>` with the CSS variables, then use `bg-[var(--bg-page)]` Tailwind arbitrary values). Alternatively use `ui.colors(primary='#8b5cf6', ...)` and the Quasar dark theme.
- **C# (Avalonia)**: register the palette as `Color` resources in `App.axaml`'s `<Application.Resources>` (or a separate `Resources/Colors.axaml`). Map to `SolidColorBrush` resources used by control templates. Use the FluentTheme as the base.

## 10. Out of scope for v1.0.x

- Light theme.
- High-contrast accessibility theme.
- Localized typography (e.g. for RTL languages).
- Animated transitions between tabs.

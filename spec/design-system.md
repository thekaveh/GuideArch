# GuideArch Design System

**Status:** Authoritative for v1.x UI work. Applies to all three impls.

The M1–M4 UIs were functional but visually unpolished. This spec defines the design language all three impls implement, so the apps look like the same product.

## 1. Personality

- **Professional**, not playful. The audience is software architects evaluating significant technology decisions.
- **Dense but not cramped.** Information per screen is high (lots of decisions, alternatives, properties), so we use compact controls and a tight type scale.
- **Two first-class themes: dark and light.** Dark is the launch default; light fully retints every token (see §2-light) and is not a degraded fallback. Both are switched via the toolbar's theme-toggle button (`AppVM.setTheme`, persisted per impl — see `spec/viewmodels.md` §3.3). OS-follow (`prefers-color-scheme`) remains out of scope (§10).
- **Quiet color**. Color carries meaning (status, criticality, fuzzy axis), not decoration.

## 2. Color tokens

Use these literal hex values. Every impl maps them into its native theming system (Tailwind for NiceGUI/Svelte, FluentTheme for Avalonia).

Both tables define the same 21 token names; impls map both and switch via `AppVM.setTheme` (`spec/viewmodels.md` §3.2). Light values are contrast-checked to WCAG AA for text-on-surface pairs.

## §2-dark — Dark theme (launch default)

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

## §2-light — Light theme

### 2.1 Surface

| Token | Hex | Use |
|---|---|---|
| `bg-page` | `#ffffff` | Page background |
| `bg-surface` | `#fbfbfd` | Cards, panels, table backgrounds |
| `bg-surface-2` | `#f3f4f8` | Elevated surfaces, modal backgrounds, hover-row highlight |
| `bg-surface-3` | `#eaecf3` | Heavily-elevated surfaces (popovers) |
| `border-subtle` | `#eef0f4` | Hairlines between rows, between panels |
| `border-strong` | `#dfe2ec` | Card borders, input borders |

### 2.2 Text

| Token | Hex | Use |
|---|---|---|
| `text-primary` | `#1a1f36` | Body text, primary labels |
| `text-secondary` | `#5a6072` | Secondary labels, captions, metadata |
| `text-muted` | `#8a90a2` | Disabled text, placeholders |
| `text-inverse` | `#ffffff` | Text on colored backgrounds |

### 2.3 Accent — single brand color (indigo-violet)

| Token | Hex | Use |
|---|---|---|
| `accent` | `#6b4ce0` | Primary buttons, focus rings, active tab indicators |
| `accent-hover` | `#5a3dd0` | Button hover |
| `accent-muted` | `#f0ecfd` | Selected-row tint, accent backgrounds |
| `accent-on` | `#ffffff` | Text on accent button |

### 2.4 Semantic (status)

| Token | Hex | Use |
|---|---|---|
| `success` | `#0ea371` | Positive status (saved, conforming) |
| `warning` | `#b8801a` | Soft warnings (lower>modal, redundant constraint, unsaved-changes chip) |
| `danger` | `#dc3a3a` | Fatal errors, destructive actions (Delete) |
| `info` | `#3a6fd8` | Informational chips |

### 2.5 Fuzzy axes (only for charts)

| Token | Hex | Use |
|---|---|---|
| `fuzzy-positive` | `#0ea371` | Positive (optimistic) vertex / dimension |
| `fuzzy-average` | `#d9a014` | Modal / average vertex |
| `fuzzy-negative` | `#e5566f` | Negative (pessimistic) vertex |

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

- 32px height (default density). 6px radius. `bg-surface-2` background. 1px `border-strong` border. Focus → the border recolours to `accent`, plus a focus ring where the platform renders one without reflow (e.g. an outset box-shadow ring). The border does **not** thicken on focus: a 1→2px bump re-measures the control and shifts dense grid cells (notably the Coefficients matrix), so the recolour carries the focus state and the ring — not added thickness — provides the emphasis. The reference (TS) uses a 1px `accent` border + a 2px box-shadow ring; C#/Python match idiomatically (Avalonia `BorderBrush`; Quasar's outlined accent border).
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
- Top-10 comparison polylines (Chart C) use the shared Tableau 10-color palette defined in `spec/charts.md` §4.

### 5.8 EmptyState

A reusable empty-state block with two variants:

- **Hero** (first-launch / dominant empty): centered vertically + horizontally; a 120×96 three-triangle illustration (see §6.2 brand identity), an uppercase kicker (`accent-hover`, 11px / 600 / 0.08em letter-spacing), a 22px / 600 headline (`text-primary`), a 14px body (`text-muted`, max 36rem), and one or two CTA buttons (Primary + optional Secondary).
- **Compact** (section empty once a scenario is loaded): smaller inline panel — 14px headline (`text-primary`), 13px body (`text-muted`), optional primary CTA. No illustration, no kicker.

The same component services both variants in TS (`EmptyState.svelte`) and Python (`_render_empty_state(hero=...)`); C# renders the hero inline in `MainWindow.axaml` for first-launch and uses ad-hoc Borders for compact empties.

### 5.9 SectionHeader

A title + one-line subtitle + optional primary action, rendered at the top of every editor tab body (Decisions / Alternatives / Properties / Coefficients / Constraints) and at the top of Results.

- Title: 16px / 600 / `text-primary` / `letter-spacing: -0.01em`.
- Subtitle (optional): 12px / `text-muted`, sits ~2px under the title.
- Action (optional): accent-filled primary button on the right.
- Bottom border: 1px `border-subtle`.
- Padding: 16/24/12/24 (t/r/b/l).

The headline copy for each tab is identical across all three impls; see `langs/typescript/src/routes/lib/*Tab.svelte` for the canonical strings.

### 5.10 ConfirmDialog (TypeScript)

A modal styled to match the design language for delete-confirmation and discard-unsaved-changes prompts, replacing the browser's native `confirm()`. Avalonia builds equivalent dialogs ad-hoc in code-behind; NiceGUI uses `ui.dialog()` — both fit their respective platform idioms, so the component is TS-only.

- Backdrop: full-screen 65% black scrim, 80 ms ease-out fade-in.
- Card: `bg-surface-3`, 1px `border-strong`, 8px radius, 20/24px padding, max-width 28rem, drop-shadow.
- Title row: icon + 15px / 600 / `text-primary` headline. Icon is a triangle-alert (danger color) for destructive prompts, info-circle (accent-hover) otherwise.
- Body: 13px / `text-secondary`, line-height 1.55, word-break.
- Buttons: right-aligned. Cancel = ghost. Confirm = accent-filled (or danger-filled when `destructive: true`).
- Keyboard: Esc cancels, Enter confirms.

## 6. Layout

- **App shell**: `bg-page` everywhere. 1px `border-subtle` separates toolbar / tab strip / main / status bar.
- **Main pane**: 24px padding around content. Vertical scroll inside tab body, not on the page.

### 6.1 Toolbar

56px tall, 24px horizontal padding, left-to-right composition:

- **Brand**: a 22×18 three-triangle motif (the same geometry as the EmptyState hero illustration, see §6.2) in `accent` color, followed by "GuideArch" in 15px / 700 / `text-primary`. 12px right margin separates the brand block from the next group.
- **File group**: New, Open…, Save, Save As… — ghost buttons with 14×14 inline-SVG icons (TS only; C# is text-only, Python uses Material icons via Quasar). 4px gap inside the group.
- **Vertical separator**: 1px / `border-subtle`, 24px tall, 6px horizontal margin.
- **Sample group**: Sample SAS, Sample EDS — accent-styled to highlight the entry points for new users.
- Flexible spacer.
- **Theme toggle**: 32×32 ghost icon button. Icon shows the *target* theme (sun while in dark, moon while in light).
- **Solve**: primary accent button on the right end.

### 6.2 Brand identity — three-triangle motif

The brand mark is a stack of three triangles of increasing density, suggesting nested decisions converging on a result. Used in three places:

| Surface | Size | Fill |
|---|---|---|
| First-launch hero illustration (`EmptyState` hero variant) | 120×96 | `accent` color at fill-opacities 0.08 / 0.16 / 0.24, full stroke |
| Toolbar wordmark | 22×18 | `accent` color at fill-opacities 0.35 / 0.60 / 0.95 |
| Tab favicons / window icons | impl-specific | impl-specific |

Path geometry is the same across impls (see `langs/typescript/src/routes/lib/EmptyState.svelte` and `Toolbar.svelte` for the canonical SVG). C# uses Avalonia `Path` with matching geometry and an `AccentFillFaintest`/`AccentFillFainter`/`AccentFillFaint` brush stack to match the hero opacity values exactly. Python embeds the SVG via `ui.html`.

### 6.3 Status bar

32px tall, 24px horizontal padding, 12px text in `text-secondary`. Renders structured segments rather than a single string. From left to right:

| Segment | Style | Visibility |
|---|---|---|
| Scenario name | `accent-hover`, 12px / 600 | When a scenario is loaded |
| File path basename | `text-muted`, 11px / mono, ellipsis if > 24rem, full path in tooltip | When `filePath` is set |
| Status text | `text-secondary`, 12px | Always |
| (flexible spacer) | | |
| Candidate count chip | info-blue chip — `"{n} candidate(s)"`, tabular numerics | When a scenario is loaded and `candidates` is non-empty |
| Unsaved chip | warning-yellow chip — `"unsaved"` | When `isDirty` |
| Warning chip | danger-red chip — `"⚠ {n} warning(s)"`, full list in tooltip | When `warnings` is non-empty |

The dirty and warning chips use **different** semantic colors (warning vs. danger) so they distinguish at a glance — an earlier revision colored both with the warning palette and conflated them.

### 6.4 Right-rail charts (Results tab)

Per `spec/charts.md` §1, the Results tab's right pane is sub-tabbed with three modes (`Ranking` / `Profile` / `Compare`). Sub-tab strip lives flush at the top of the right pane (40px tall, identical typography to the main tab strip's inactive state but with a single 2px accent indicator and rounded background), and only the active chart is mounted — preserving plot state across switches.

## 7. Motion

- Minimal. Hover transitions on background-color over 120ms ease-out. No bouncing, no spinning, no entrance animations.
- Toast/notification appears with 200ms ease-out from top, auto-dismisses after 3s.

## 8. Empty states

Two variants, both realized by the `EmptyState` component (§5.8):

### 8.1 First-launch hero (dominant)

Shown when no scenario is loaded. Dominates the tab body regardless of which tab the user landed on first, so the user always sees the same welcome page.

- Centered vertically + horizontally in the available area.
- 120×96 three-triangle illustration (brand identity, §6.2) above the text block.
- Uppercase kicker `"Welcome to GuideArch"` in `accent-hover` (11px / 600 / 0.08em letter-spacing). All three impls keep the kicker data pristine ("Welcome to GuideArch") and uppercase it at render time: TS and Python via CSS `text-transform: uppercase`; C# via a `ToUpperConverter` (`langs/csharp/src/GuideArch.View/Converters.cs`) since Avalonia `TextBlock` has no `text-transform` analogue. Uppercasing uses invariant culture to avoid the Turkish dotted-I/dotless-i mapping breaking the brand string on Turkish locales.
- Headline (22px / 600 / `text-primary`): `"Pick a software architecture, with fuzzy TOPSIS."`
- Body (14px / `text-muted`, max 36rem, line-height ~1.55): explains what the app does and points the user at the sample CTAs.
- Two primary CTAs: `Open Sample SAS` (accent fill) and `Open Sample EDS` (secondary).

### 8.2 Compact section empty (subordinate)

Shown when a scenario IS loaded but a section has no rows yet (e.g. Critical Decisions / Critical Constraints with zero entries, Coefficients matrix without alternatives or properties, etc.).

- Centered vertically in the available area.
- No illustration, no kicker.
- 14px `text-primary` headline; 13px `text-muted` body.
- Optional primary CTA — e.g. `+ Add Property` on a Properties tab with zero rows.

## 9. Per-impl realization notes

- **TypeScript (Svelte)**: define the tokens as CSS custom properties on `:root` in a global `app.css` (or `+layout.svelte`). Inject before `+page.svelte` mounts. Components reference `var(--bg-page)` etc. Light theme is realized as a `[data-theme='light']` override block in `app.css` that redefines all 21 custom properties to the §2-light values.
- **Python (NiceGUI / Quasar)**: NiceGUI exposes Tailwind 4 classes; map the tokens via `tailwind.config.js` (`ui.add_head_html` to inject a `<style>` with the CSS variables, then use `bg-[var(--bg-page)]` Tailwind arbitrary values). Alternatively use `ui.colors(primary='#8b5cf6', ...)` and the Quasar dark theme. Light theme is realized as `body.body--light` CSS-variable overrides injected by `theme.py`, driven by `ui.dark_mode()`.
- **C# (Avalonia)**: register the palette as `Color` resources in `App.axaml`'s `<Application.Resources>` (or a separate `Resources/Colors.axaml`). Map to `SolidColorBrush` resources used by control templates. Use the FluentTheme as the base. Light theme is realized via `Colors.axaml` `ThemeDictionaries` (`Dark`/`Light`) so all brushes are consumed via `DynamicResource` and switch atomically when the active dictionary changes.

## 10. Out of scope for v1.0.x

- High-contrast accessibility theme.
- OS-follow theme variant (`prefers-color-scheme`).
- Localized typography (e.g. for RTL languages).
- Animated transitions between tabs.

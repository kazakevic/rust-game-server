# Web Admin — Design System & Views

The UI is server-rendered HTML built from template-string functions. There is no build
step — Tailwind is loaded via the browser CDN and styled with a light, shadcn-inspired
theme.

## `views/layout.ts`

`layout(title, content, { activePage })` returns the full HTML document: `<head>`
(Tailwind browser CDN + `@theme` tokens `--color-primary: #18181b`,
`--color-accent: #cd412b`, console scrollbar styles), the sticky top nav, and the
`<main>` container. The nav has top-level links (Dashboard, Players) plus hover
**dropdowns** built by `navDropdown`/`dropdownLink`:
- **Server** → Settings, Console, Server Logs, Web Logs, Config Files
- **Plugins** → uMod Plugins, NPC Manager, Stack Sizes

`activePage` highlights the current item.

## `views/components.ts`

Reusable, escaped HTML component functions (the shared building blocks for all pages):

- `escapeHtml(s)` — HTML-escape untrusted strings.
- `badge(text, variant)` — `default | success | destructive | warning | outline`.
- `alert(message, variant)` — banner: `error | success | warning | info` (with icon).
- `card({ title, description, headerRight, class }, content)` — bordered card w/ header.
- `statsCard(label, value, { icon, detail })` — dashboard metric tile.
- `button(text, { variant, size, type, attrs })` — buttons/links.
- `input` / `textarea` / `checkbox` / `select` — form controls.
- `section(title, content, { description })` — labeled content block.
- `pageHeader(title, { description, actions })` — page title row.
- `statusDot(active)` — green/red pulse indicator.
- `emptyState(message, { icon })` — empty-list placeholder.
- `modal(id, title, content)` — overlay dialog.

> When adding UI, reuse these components and the existing light theme rather than
> introducing new styling patterns. Per project frontend rules: password inputs need a
> show/hide toggle, and search inputs should wire a Cmd+K / `/` shortcut.

## Page modules (`views/*.ts`)

One module per page, each exporting a `*Page(...)` function that composes components
inside `layout()`:

| Module | Page |
|---|---|
| `login.ts` | Login form |
| `dashboard.ts` | Status/stats tiles + server/world/weather controls |
| `rcon.ts` | RCON console + searchable command library (largest view) |
| `players.ts` | Online players + teleport |
| `npcs.ts` | NPC Manager |
| `stacksize.ts` | StackSizeController editor |
| `settings.ts` | Server settings form (exports `ServerSettings` type) |
| `configs.ts` | Config file list + editor (`configsListPage`, `configsEditPage`) |
| `logs.ts` | Server (container) logs |
| `web-logs.ts` | In-memory web log viewer |
| `plugins.ts` | uMod plugin list manager |

What each page does functionally is documented in [features.md](features.md).

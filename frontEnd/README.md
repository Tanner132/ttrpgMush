# Seattle by Night — Frontend

A Vite + React 19 + TypeScript single-page client for the Seattle by Night MUSH.
Routed with React Router 7; realtime transport via `@microsoft/signalr`.

## Commands

```powershell
npm run dev       # start the Vite dev server (proxies /api and /hubs)
npm run test      # run the Vitest suite
npm run lint      # oxlint
npm run build     # typecheck + production build
npm run gen:api   # regenerate src/api/generated from the backend OpenAPI document
```

`src/api/generated` is produced from
`../backEnd/src/SeattleByNight.Api/openapi/SeattleByNight.Api.json`. Rerun
`gen:api` and commit the result after any backend contract change; CI fails on a
stale copy.

## Routes

| Route | Page | Access |
| --- | --- | --- |
| `/login` | `LoginPage` | Anonymous |
| `/characters` | `CharactersPage` (slot dashboard) | Authenticated |
| `/characters/create`, `/characters/create/:characterId` | `MethodSelectionPage`, `CreatorShellPage` | Authenticated |
| `/characters/:characterId/sheet` | `CharacterSheetPage` | Authenticated owner |
| `/play` | `GameplayPage` | Active play session |
| `/admin/users`, `/admin/audit` | `AdminUsersPage`, `AdminAuditPage` | `Administrator` |
| `/admin/world`, `/admin/forge` | `WorldEditorPage`, `WorldForgePage` | `Administrator`, `WorldBuilder` |

Route guards are a convenience; the server is authoritative.

## Folder conventions

| Path | Responsibility |
| --- | --- |
| `src/pages` | Route-level pages, including `admin/` and `characterCreation/` |
| `src/components` | Reusable UI and page-composed sections |
| `src/components/ui` | Design-system primitives (`Button`, `Modal`, `Panel`, `Tabs`, …) |
| `src/components/characterCreation` | Creator shell, catalog helpers, and one component per step under `steps/` |
| `src/components/careerSheet` | The shared character sheet used by both the route and the `/character` modal |
| `src/components/worldForge` | World Forge editors and the shared draft/save/publish hook (`useContentDraft`) |
| `src/commands` | Composer command registry, parser, and dispatcher |
| `src/hooks` | Gameplay lifecycle hooks (session, presence, transcript, idle) |
| `src/realtime` | SignalR client and hooks |
| `src/api` | REST clients and generated API types |
| `src/auth` | Account restoration context |
| `src/styles` | Design tokens, base styles, and per-component styles |

Conventions:

- The frontend never calculates rules values (costs, maximums, pools, prices).
  It renders what the server returns.
- A World Forge editor that owns part of a content fragment must round-trip the
  rest of the fragment untouched.
- When a design mockup shows a control with no backing functionality, build it
  as inert markup; do not invent behavior for it.

## Visual language

The interface uses a deliberate retro-future neon-noir system defined entirely
by semantic CSS custom properties in [`src/styles/tokens.css`](src/styles/tokens.css).
Components reference tokens, never raw colors.

### Surfaces

- `--sb-bg` (`#0b0c10`) is the deep-obsidian application backdrop.
- `--sb-bg-inset` (`#000000`) is reserved for inset display surfaces such as the
  transcript feed.
- Panels and controls use `--sb-surface` / `--sb-surface-raised` with
  `--sb-border` / `--sb-border-strong` strokes.

### Text

- `--sb-text` is a softened green-tinted off-white used for long-form prose.
- `--sb-text-muted` and `--sb-text-faint` are subdued secondary/disabled text.

### Semantic accents

Neon is an accent and semantic signal, never body-copy color. Meaning is never
communicated through color alone.

| Token | Value | Meaning |
| --- | --- | --- |
| `--sb-accent` | `#00ffcc` (emerald) | active / positive |
| `--sb-warning` | `#ffb000` (amber) | caution |
| `--sb-danger` | `#ff0055` (magenta) | danger / critical |
| `--sb-info` | `#00d2ff` (cyan) | informational / future Matrix |

### Typography

Rajdhani (headings and UI labels) and Source Code Pro (transcript, commands, and
data-dense text) are self-hosted; see [`fonts/README.md`](fonts/README.md).

### Angular aesthetic and motion

- Sharp corners with a restrained 45-degree cut on non-interactive surfaces;
  interactive controls keep rectangular boxes so focus outlines are never clipped.
- Motion tokens (`--sb-duration-*`, `--sb-ease`) govern only short, meaningful
  transitions. A finite glitch plays solely when a connection drops, and all
  decorative motion is disabled under `prefers-reduced-motion`.
- A faint scanline treatment lives only on the backdrop; the transcript and form
  fields use opaque surfaces so it never overlaps content.

# Milestone 2B: Retro-Future Neon-Noir Visual Foundation

**Outcome:** The routed frontend uses an accessible, responsive visual system inspired
by a cyberdeck cockpit while preserving the usability of long-form text roleplay.
The milestone restyles existing capabilities only; it does not invent unavailable
gameplay data or client-side authority.

**Depends on:** Milestone 2.

See [`../ROADMAP.md`](../ROADMAP.md) for shared delivery rules and verification commands.

## Approved Direction

- Build the visual system with project-owned React components and CSS custom properties. Do not add Arwes, Termino.js, Cyberpunk UI, or another component/UI framework.
- Arwes and the named commercial games may be used as mood references only. Do not copy proprietary assets, exact layouts, iconography, sounds, or distinctive trade dress.
- Use deep obsidian for the application backdrop and reserve true black for inset displays such as the transcript.
- Use softened high-contrast text for long-form reading. Saturated neon colors are accents and semantic signals, not body-copy colors.
- Use sharp corners and restrained 45-degree cuts rather than rounded cards, while preserving focus outlines and comfortable control hit areas.
- Keep the main feed visually dominant. The cockpit must not become decorative chrome around an undersized transcript.
- Do not render placeholder condition monitors, Edge, inventory, weapons, combat state, or other data that the server does not provide.
- Do not add Matrix, combat, or other channel tabs until those channels have server-defined semantics.
- Do not add a player map beyond the authoritative current exits in this milestone. Coordinates remain presentation metadata and exits remain the source of truth for connectivity.

## UI-201: Define Design Tokens, Typography, And Motion Rules

**Depends on:** FE-206.

**Scope:**

- Replace the adaptive generic light/dark palette with a deliberate dark visual system expressed through semantic CSS custom properties.
- Start from `#0b0c10` for the application backdrop and `#000000` for inset feed surfaces.
- Use a softened green-tinted off-white for primary text and a subdued contrasting color for secondary text; validate final values rather than using fully saturated neon for prose.
- Define semantic accent tokens based on emerald `#00ffcc`, amber `#ffb000`, magenta `#ff0055`, and cyan `#00d2ff`.
- Use emerald for active/positive state, amber for caution, magenta for danger/critical state, and cyan for informational or future Matrix-oriented state. Never communicate meaning through color alone.
- Self-host Rajdhani for headings and Source Code Pro for transcript, commands, and data-dense text using WOFF2 assets with appropriate fallbacks and `font-display` behavior.
- Define spacing, border, angular-cut, glow, focus, elevation, transcript-width, and motion-duration tokens.
- Define a no-flicker baseline and reduced-motion behavior before adding any animation.

**Acceptance criteria:**

- Token names describe semantic purpose rather than a specific raw color.
- Normal text, muted text, controls, focus indicators, warnings, errors, and disabled states meet applicable WCAG AA contrast requirements.
- The interface remains usable if custom fonts fail to load.
- Font licenses are compatible, retained with the project as required, and documented.
- `prefers-reduced-motion` removes nonessential animation and transitions.
- A short visual-language section in the frontend documentation records approved token usage and semantic colors.

## UI-202: Build Accessible Cyberdeck UI Primitives

**Depends on:** UI-201.

**Scope:**

- Create reusable primitives only where current pages need them: application frame, panel, heading/label, button, form control, status banner, tabs, and inset feed surface.
- Implement angular corners and clipped accents with CSS, pseudo-elements, or small presentational wrappers rather than a UI framework.
- Preserve semantic HTML and native control behavior. Decorative frames must not replace buttons, headings, labels, lists, dialogs, or landmarks.
- Keep decorative layers non-interactive and out of the accessibility tree.
- Provide consistent hover, active, focus-visible, disabled, validation, loading, and selected states.
- Keep component APIs semantic; callers choose intent such as `primary`, `info`, `warning`, or `danger` rather than raw neon colors.

**Acceptance criteria:**

- Every interactive primitive is keyboard accessible and retains a clearly visible focus indicator despite clipping or pseudo-elements.
- Controls meet practical pointer target sizing on desktop and touch devices.
- Primitive tests cover semantics, keyboard interaction, disabled state, and accessible names where applicable.
- No new runtime UI or terminal-emulation dependency is added.
- The primitives can style login, character selection, gameplay, and later administrative pages without embedding gameplay logic.

## UI-203: Apply The Responsive Cyberdeck Cockpit

**Depends on:** UI-202.

**Scope:**

- Restyle the shared application shell, login page, character page, and gameplay page using the approved primitives and tokens.
- Make the gameplay feed the primary cockpit surface containing room description, transcript, and composer.
- Use the secondary cockpit area only for data already available: exits, occupants/presence, connection status, selected character, and session-expiry warning.
- On narrow viewports, render the main feed first and place secondary information in accessible stacked or collapsible sections without hiding essential movement or session controls.
- Preserve transcript scrolling, older-message pagination, reconnect behavior, command input readiness, and room movement.
- Keep the transcript measure, line height, spacing, and contrast comfortable for extended reading sessions.
- Render current exits as the available spatial/navigation model. Do not imply that visual position determines connectivity.

**Acceptance criteria:**

- All existing flows remain functional after the redesign: registration, login, character creation/selection, transcript history, chat, presence, movement, reconnect, expiry warning, and logout.
- Desktop, tablet, and narrow mobile layouts expose all required controls without horizontal page scrolling.
- The transcript remains visually dominant and readable at browser zoom up to 200%.
- Secondary panels do not contain fake values, disabled feature teasers, or empty futuristic widgets.
- Automated interaction tests pass, and key pages receive focused responsive-layout regression coverage where practical.

## UI-204: Add Restrained Effects And Complete Accessibility Review

**Depends on:** UI-203.

**Scope:**

- Add an optional-looking but CSS-native static scanline treatment to surrounding chrome or background surfaces at very low opacity.
- Keep scanlines absent from, or nearly imperceptible over, the main transcript and form fields.
- Permit brief glitch effects only when tied to a meaningful implemented event. Do not add continuous flicker, random glitch loops, automatic sound, CRT curvature that distorts text, or typing animations that delay content.
- Ensure all decorative motion is disabled by `prefers-reduced-motion` and content remains immediately available without animation.
- Review keyboard order, landmarks, labels, live status announcements, zoom, contrast, forced-colors resilience, and screen-reader output.
- Measure the added font and CSS cost and remove effects that materially harm initial rendering or interaction responsiveness.

**Acceptance criteria:**

- No effect flashes rapidly, obstructs text selection, intercepts pointer input, delays content, or changes layout measurements.
- Reduced-motion users receive a stable interface with no glitch or decorative transition dependence.
- Connection, error, warning, and session-expiry states remain understandable without color, glow, or animation.
- Login, character selection, and gameplay complete a keyboard-only and screen-reader smoke test.
- Frontend tests, lint, and build pass after final style cleanup.

## Deferred Visual Features

- Add feed filters only after typed messages exist. The initial typed filters should represent real categories only, keep `All` as the default, and indicate when an active filter hides new entries.
- Add condition monitors, Edge, equipment, and combat presentation only with their future server-authoritative systems.
- Add Matrix styling and channels only after Matrix semantics are designed.
- Add a player-facing topology or coordinate map only through a separately approved feature that preserves directed exits as connectivity authority.

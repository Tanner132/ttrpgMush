# Fonts

The visual system self-hosts two open-source typefaces via the
`@fontsource` packages (bundled at build time by Vite; no runtime CDN
requests are made).

| Typeface | Role | Weights | Copyright |
| --- | --- | --- | --- |
| Rajdhani | Headings and UI labels | 500, 600, 700 (latin) | Copyright (c) 2014 Indian Type Foundry (`info@indiantypefoundry.com`) |
| Source Code Pro | Transcript, commands, and data-dense text | 400, 600 (latin) | Google Inc. |

Both typefaces are distributed under the SIL Open Font License, Version 1.1.
The full license text is retained in [`OFL.txt`](./OFL.txt). The interface
falls back to system UI and monospace stacks if these fonts fail to load.

---
name: testing-setup
description: How frontend verifies Gurka in real browsers - isolated server port, puppeteer-core with Edge, pitfalls
metadata:
  type: project
---
Verify the client against a real server with real browser clients, not by reading code.

- Program.cs hardcodes `UseUrls("http://0.0.0.0:5000")`; run an isolated instance with `dotnet build -o <scratch>/srv` then `dotnet Gurka.dll --contentRoot <repo> --Kestrel:Endpoints:Http:Url=http://127.0.0.1:5077` (Kestrel config overrides UseUrls). Start it with PowerShell `Start-Process` – a Bash background job got killed once. wwwroot edits are live without restart.
- Browser: puppeteer-core + Edge at `C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe`. One `createBrowserContext()` per player so storage is separate.
- Rooms: the server rejects unknown room codes ("Bordet finns inte"); create one with `hub.invoke('NewRoom')` first, then open `/?bord=CODE`.
- Pitfall: `setViewport` that toggles `isMobile`/`hasTouch` reloads the page (kills injected bots). Keep them constant.
- Narrow widths: emulated viewport works below 500 px; the iframe check (host page on same origin, e.g. `/health`) also works since CSP has `frame-ancestors 'self'`.
- Layout checker: elements inside scroll containers (#past) show up as "outside the viewport" – false positives.
- Flood filter limits Hello/NewRoom per IP, so do not open dozens of tabs quickly.

**Why:** first full run found bugs (play button hidden on forced sak, fan CSS var bug) that code reading missed.
**How to apply:** reuse this setup for every UI change; see [[design-decisions]].

- Fastest render check: open `/`, wait for `hub.state==='Connected'`, call `A.newRoom()` once, `hub.off('state')`, then assign a hand-built `S={...}` and call `render(null)`. Reuse one page and loop `setViewport` sizes – avoids the NewRoom flood limit. Script template kept in an old scratchpad (`pt/tie.js`); recreate if gone.
- Pitfall: app.js is one classic script with top-level `const`s – a new helper name that collides (e.g. `tie`, already used for draw text) kills the whole client with "Identifier already declared". Run `node --check` AND grep the name before adding helpers.
- Overlap checker (2026-10-17): for every phase x size, compare rects of #msg text (Range over contents, not the box), .play and .felt .plate; plus scrollWidth. Old scratchpad `pt/ovl.js`. Visual review still needed: a too-small card (22 px) passes the checker.
- Compare before/after by `git stash -- wwwroot/app.css`, run, `stash pop`.

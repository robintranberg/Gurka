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
- 2026-10-17: scenario-driven e2e for 1 upp in `pt/up.js` (args N scenario: take/other/allno/other2/keep2) with a leak check (no other player's hand card – except revealed/tie/up cards – in anyone's S or #felt/#log). Hello flood limit hits after ~2 runs back-to-back: wait ~45 s between runs (use run_in_background with sleep). Harness pitfall: fake states built with `{...S}` inherit test-only fields from the previous fake state – reset them.
- Server-generated View-JSON fixtures (ask tester: they dump Game.View from a rigged deck) catch states that real play can't reach (e.g. 1 upp with deck 0). Render them with `pt/fix.js` (assign S, render(null)). On 2026-10-17 they caught texts that offered choices the deck didn't allow – make action texts depend on the same condition as the buttons.
- Codespaces links (`*.app.github.dev`) show an interstitial "Codespaces Access Port" page with a Continue button first; click it, then SignalR over wss works. Bug reports from the user's codespace: first check that the server process was restarted after the pull – `dotnet run` serves wwwroot live from disk, so a new client against an old server binary is a real failure mode (2026-10-17: "1 upp gives a new card directly" = new client + pre-1upp server). Reproduce by building the old commit in a `git worktree` and running it with `--contentRoot <repo>`.

---
name: design-decisions
description: Gurka client design decisions and constraints (deck info hidden, card faces, CSP-safe markup, http-on-LAN limits)
metadata:
  type: project
---
- The user chose to hide deck info (commit 4905e05): show deck/slask counts only in the respond phase when the deck has under 7 cards. Do not add a deck counter.
- Real card faces (corner index + big pip bottom right, hidden by the next card in the hand) were approved in the responsive redesign (c469e90). Keep that look.
- Leading: only cards of the same rank can be selected (commit 4c49b96) – dim the others.
- 2026-10-16 modern redesign: split into index.html/app.css/app.js, SignalR local in wwwroot/lib, no inline JS, no on* attributes, no style attributes in generated HTML (use CSSOM/WAAPI) so CSP can be `script-src 'self'; style-src 'self'`. Buttons use `data-a` + one delegated click handler.
- Players are reached over plain http on the LAN, so `crypto.randomUUID`, service workers, Wake Lock and clipboard are unavailable there (secure-context only). Feature-detect; never rely on them.
- Seat token is server-issued and rotated on every `Hello`; sessionStorage per tab + localStorage map {t,s} (visible beat, hidden, released) – only released, silent >6 s or hidden >20 s tokens are reused so a second tab never steals a live seat.

**Why:** these were user choices or hard platform limits found while building.
**How to apply:** check this before changing visuals or connection code. Related: [[testing-setup]].
